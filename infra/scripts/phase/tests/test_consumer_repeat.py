"""Synthetic tests of the consumer repeat sequence (plan consumer-jobs, tests T8 to T16c): a scripted command runner and a fake clock."""
import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parent))
import test_phase as shared  # noqa: E402  (constants and fixtures only; its test classes are not imported)

REPO = Path(__file__).resolve().parents[4]
phase = shared.phase
SPEC = importlib.util.spec_from_file_location('consumer_repeat', REPO / 'infra/scripts/phase/consumer_repeat.py')
consumer_repeat = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(consumer_repeat)

RUN = 'c03-40121134428-1'
MEMBER_MODES = ('consumer-subject', 'consumer-witness')
SIX = ['consumer-subject', 'consumer-witness', 'consumer-readiness', 'start-line', 'loadgen', 'verify']
DEADLINES = {'consumer-storage-check': 120, 'consumer-subject': 1800, 'consumer-witness': 1800, 'start-line': 150, 'loadgen': 1140, 'verify': 750, 'judge': 90}
START = 1000.0
PODS = ('pod-a', 'pod-b')


def event(partitions, kind='assigned'):
    return json.dumps({'schema_version': 1, 'kind': 'partition_event', 'instance_id': 'x', 'row_sequence': 1,
                       'event': kind, 'partitions': partitions, 'event_utc': '2026-11-16T00:00:00.000Z'}) + '\n'


def ready_layout():
    return {PODS[0]: [event(list(range(16)))], PODS[1]: [event(list(range(16, 32)))]}


class Fake:
    """A scripted command runner. It records every command and can advance the fake clock on selected commands."""
    def __init__(self, test):
        self.test = test
        self.calls = []
        self.responses = {}
        self.layout = lambda poll: ready_layout()
        self.polls = {}
        self.blobs = {}
        self.hooks = []
        self.logs = {}  # storage check job name -> pod log text; an attempt with no entry succeeds

    def check_log(self, job):
        return self.logs.get(job, 'consumer_storage_check result=ok\n')

    def __call__(self, instance, args, seconds, output=None, maximum=4 * 1024 * 1024):
        args = list(map(str, args))
        self.calls.append((tuple(args), seconds, self.test.now))
        default = (0, '')
        if args[0] == 'helm':
            values = json.loads(Path(args[-1]).read_text())
            runtime = values['runtime']
            name = phase.job_name(runtime['run_id'], runtime['phase'], runtime['mode'], runtime.get('repeat'), values['scenario_id'], values['configuration_id'])
            default = (0, f'kind: Job\nmetadata:\n  name: "{name}"\nspec:\n  activeDeadlineSeconds: {DEADLINES[runtime["mode"]]}\n')
        elif args[:3] == ['kubectl', 'get', 'pods']:
            default = (0, '{"items": []}')
        elif args[:2] == ['kubectl', 'logs']:
            default = (0, self.check_log(args[2].removeprefix('job/')))
        elif args[:4] == ['az', 'storage', 'blob', 'list']:
            prefix = args[args.index('--prefix') + 1]
            number = self.polls[prefix] = self.polls.get(prefix, 0) + 1
            names = []
            for pod, lines in self.layout(number).items():
                names.append(prefix + pod + '/registration.json')
                self.blobs[names[-1]] = b'{}\n'
                if lines is not None:
                    names.append(prefix + pod + '/record/000000.jsonl')
                    self.blobs[names[-1]] = ''.join(lines).encode()
            default = (0, json.dumps([{'name': name} for name in names]))
        elif args[:4] == ['az', 'storage', 'blob', 'show']:
            content = self.blobs[args[args.index('--name') + 1]]
            default = (0, json.dumps({'etag': 'synthetic-etag', 'contentLength': len(content)}))
        elif args[:4] == ['az', 'storage', 'blob', 'download']:
            Path(args[args.index('--file') + 1]).write_bytes(self.blobs[args[args.index('--name') + 1]])
        for hook in self.hooks:
            hook(args)
        code, content = self.responses.get(tuple(args), default)
        if output is not None:
            Path(output).write_bytes(content.encode() if isinstance(content, str) else content)
        return code


class ConsumerRepeatTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for name in ('env', 'output', 'summary'):
            (self.root / name).touch()
        env = {'PATH': os.environ.get('PATH', ''), 'RUNNER_TEMP': str(self.root), 'GITHUB_ENV': str(self.root / 'env'),
               'GITHUB_OUTPUT': str(self.root / 'output'), 'GITHUB_STEP_SUMMARY': str(self.root / 'summary'),
               'GITHUB_RUN_ID': '40121134428', 'GITHUB_RUN_ATTEMPT': '1', 'GITHUB_SHA': shared.SHA,
               'PARCHWELL_REPOSITORY': str(REPO), 'PARCHWELL_CLUSTER_RESOURCE_ID': shared.RESOURCE,
               'S_CLUSTER_BACKEND': 'synthetic-backend', 'S_CLUSTER_TFVARS': '{}', 'S_EPHEMERAL_BACKEND': 'synthetic-backend',
               'S_EPHEMERAL_TFVARS': '{}', 'S_PERSISTENT_OUTPUTS': json.dumps(shared.PERSISTENT), 'PARCHWELL_SCENARIO': 'C03'}
        environment = patch.dict(os.environ, env, clear=True)
        environment.start()
        self.addCleanup(environment.stop)
        self.now = START
        self.fake = Fake(self)
        self.assertEqual(0, self.main('prepare', 'C0'))
        os.environ.update(dict(line.split('=', 1) for line in (self.root / 'env').read_text().splitlines()))
        self.private = Path(os.environ['PARCHWELL_PRIVATE_DIRECTORY'])
        self.artifacts = Path(os.environ['PARCHWELL_ARTIFACT_DIRECTORY'])
        state = json.loads((self.private / 'phase.json').read_text())
        digests = {**shared.DIGESTS, 'enrichment': 'sha256:' + 'd' * 64}
        state['images'] = {worker: {'digest': digest, 'reference': f'parchwellacr.azurecr.io/parchwell/{worker}@{digest}'} for worker, digest in digests.items()}
        (self.private / 'phase.json').write_text(json.dumps(state))
        accounts = {**shared.ACCOUNTS, 'enrichment-consumer': {'name': 'enrichment-consumer', 'client_id': 'synthetic-enrichment', 'subject': 'system:serviceaccount:parchwell:enrichment-consumer'}}
        (self.private / 'cluster-output.json').write_text(json.dumps({'workload_identity': {'value': {'namespace': 'parchwell', 'service_accounts': accounts}}}))
        (self.private / 'ephemeral-output.json').write_text(json.dumps(shared.EPHEMERAL))
        self.run_id = state['run_id']

    def main(self, *argv):
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return phase.main(list(argv))

    def mocked(self):
        stack = contextlib.ExitStack()
        stack.enter_context(patch.object(phase.Phase, 'run', autospec=True, side_effect=self.fake))
        stack.enter_context(patch.object(phase.Phase, 'sleep', side_effect=self.sleep))
        stack.enter_context(patch.object(time, 'monotonic', side_effect=lambda: self.now))
        return stack

    def sleep(self, seconds):
        self.now += seconds

    def run_repeats(self):
        with self.mocked():
            return self.main('run-repeats')

    def records(self, repeat=None):
        modes = json.loads((self.artifacts / 'phase-status.json').read_text())['modes']
        return [record for record in modes if repeat is None or record['repeat'] == repeat]

    def name(self, mode, repeat):
        return phase.job_name(self.run_id, 'C0', mode, repeat)

    def apply(self, mode, repeat):
        return ('kubectl', 'apply', '-f', str(self.private / f'rendered-{self.name(mode, repeat)}.yaml'), '--request-timeout=30s')

    def wait(self, mode, repeat, seconds):
        return ('bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job', self.name(mode, repeat), str(seconds))

    def labels(self, repeat):
        """The command sequence of one repeat as short labels. Blob downloads are left out; every list command is one poll."""
        names = {self.name(mode, repeat): mode for mode in (*DEADLINES, *MEMBER_MODES) if mode != 'consumer-storage-check'}
        labels = []
        for args, _, _ in self.fake.calls:
            if args[0] == 'helm':
                job = json.loads(Path(args[-1]).read_text())['runtime']
                if job.get('repeat') == repeat and job['mode'] != 'consumer-storage-check':
                    labels.append(('render', job['mode']))
            elif args[:2] == ('kubectl', 'apply') and Path(args[3]).stem.removeprefix('rendered-') in names:
                labels.append(('apply', names[Path(args[3]).stem.removeprefix('rendered-')]))
            elif args[:4] == ('az', 'storage', 'blob', 'list') and f'/r{repeat}/' in args[args.index('--prefix') + 1]:
                labels.append(('list',))
            elif args[:3] == ('bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job') and args[3] in names:
                labels.append(('wait', names[args[3]]))
            elif args[:3] == ('bash', 'infra/scripts/lifecycle/lifecycle.sh', 'collect-job') and args[3] in names:
                labels.append(('collect', names[args[3]]))
            elif args[:3] == ('kubectl', 'delete', 'job') and args[3] in names:
                labels.append(('delete', names[args[3]]))
            elif args[:3] == ('kubectl', 'get', 'pods') and args[6].removeprefix('job-name=') in names:
                labels.append(('confirm', names[args[6].removeprefix('job-name=')]))
        return labels

    def count_calls_after(self, mark):
        return len(self.fake.calls) - mark

    # T8
    def test_every_command_succeeds(self):
        self.fake.layout = lambda poll: ready_layout() if poll >= 3 else {PODS[0]: [event(list(range(16)))]}
        self.assertEqual(0, self.run_repeats())
        for repeat in range(1, 6):
            expected = [('render', 'consumer-subject'), ('apply', 'consumer-subject'), ('render', 'consumer-witness'), ('apply', 'consumer-witness'),
                        ('list',), ('list',), ('list',),
                        ('render', 'start-line'), ('apply', 'start-line'), ('wait', 'start-line'), ('collect', 'start-line'),
                        ('render', 'loadgen'), ('apply', 'loadgen'), ('wait', 'loadgen'), ('collect', 'loadgen'),
                        ('wait', 'consumer-subject'), ('collect', 'consumer-subject'), ('wait', 'consumer-witness'), ('collect', 'consumer-witness'),
                        ('render', 'verify'), ('apply', 'verify'), ('wait', 'verify'), ('collect', 'verify')]
            self.assertEqual(expected, self.labels(repeat), repeat)
            self.assertEqual(SIX, [record['mode'] for record in self.records(repeat)])
        records = self.records()
        self.assertEqual(30, len(records))
        self.assertEqual(['complete'] * 5, [r['outcome'] for r in records if r['mode'] == 'consumer-subject'])
        readiness = [r for r in records if r['mode'] == 'consumer-readiness']
        self.assertEqual([('ready', 3, 10, 'ready')] * 5, [(r['outcome'], r['polls'], r['elapsed_seconds'], r['last_condition']) for r in readiness])
        self.assertTrue(all(r['outcome'] == 'complete' for r in records if r['mode'] != 'consumer-readiness'))
        self.assertTrue(all(r['wait'] == 0 and r['collect'] == 0 for r in records if r['mode'] in MEMBER_MODES))

    def start_one_of_two_fails(self, subject_applies, witness_applies, repeat=2):
        subject_job = self.apply('consumer-subject', repeat)
        witness_job = self.apply('consumer-witness', repeat)
        if not subject_applies:
            self.fake.responses[subject_job] = (1, '')
        if not witness_applies:
            self.fake.responses[witness_job] = (1, '')

    # T9 (i)
    def test_subject_apply_failure_does_not_run_witness(self):
        self.start_one_of_two_fails(False, True)
        self.assertEqual(1, self.run_repeats())
        self.assertEqual([('render', 'consumer-subject'), ('apply', 'consumer-subject'), ('delete', 'consumer-subject'),
                          ('confirm', 'consumer-subject'), ('delete', 'consumer-witness'), ('confirm', 'consumer-witness')], self.labels(2))
        records = {r['mode']: r for r in self.records(2)}
        self.assertEqual(SIX, [r['mode'] for r in self.records(2)])
        self.assertEqual('apply_failed', records['consumer-subject']['outcome'])
        self.assertEqual(('not_attempted', 'consumer_apply_failed', None), (records['consumer-witness']['outcome'], records['consumer-witness']['reason'], records['consumer-witness']['apply']))
        for mode in SIX[2:]:
            self.assertEqual(('not_attempted', 'consumer_apply_failed'), (records[mode]['outcome'], records[mode]['reason']), mode)
        self.assertEqual(6, len(self.records(3)))
        self.assertIn(('wait', 'verify'), self.labels(3))

    # T9 (ii)
    def test_witness_apply_failure_collects_subject_first(self):
        self.start_one_of_two_fails(True, False)
        self.assertEqual(1, self.run_repeats())
        self.assertEqual([('render', 'consumer-subject'), ('apply', 'consumer-subject'), ('render', 'consumer-witness'), ('apply', 'consumer-witness'),
                          ('collect', 'consumer-subject'), ('delete', 'consumer-subject'), ('confirm', 'consumer-subject'),
                          ('delete', 'consumer-witness'), ('confirm', 'consumer-witness')], self.labels(2))
        records = {r['mode']: r for r in self.records(2)}
        subject = records['consumer-subject']
        self.assertEqual((0, None, 'not_attempted', 'consumer_apply_failed', 0), (subject['apply'], subject['wait'], subject['outcome'], subject['reason'], subject['collect']))
        self.assertEqual('apply_failed', records['consumer-witness']['outcome'])
        for mode in SIX[2:]:
            self.assertEqual(('not_attempted', 'consumer_apply_failed'), (records[mode]['outcome'], records[mode]['reason']), mode)
        self.assertEqual(6, len(self.records(3)))

    # T9 (iii)
    def test_subject_collect_codes_when_witness_apply_fails(self):
        for code, gap, outcome in ((1, True, 'not_attempted'), (2, None, 'collect_error')):
            with self.subTest(code=code):
                self.setUp()
                self.start_one_of_two_fails(True, False)
                self.fake.responses[('bash', 'infra/scripts/lifecycle/lifecycle.sh', 'collect-job', self.name('consumer-subject', 2))] = (code, '')
                self.run_repeats()
                subject = next(r for r in self.records(2) if r['mode'] == 'consumer-subject')
                self.assertEqual((gap, outcome), (subject.get('collect_gap'), subject['outcome']))

    # T10
    def test_readiness_never_ready(self):
        self.fake.layout = lambda poll: {PODS[0]: [event(list(range(16)))]}
        self.assertEqual(1, self.run_repeats())
        polls = [started for args, _, started in self.fake.calls if args[:4] == ('az', 'storage', 'blob', 'list') and '/r1/' in args[args.index('--prefix') + 1]]
        self.assertEqual(60, len(polls))
        self.assertEqual(295.0, max(polls) - polls[0])
        self.assertEqual(polls[0] + 295, polls[-1])
        readiness = next(r for r in self.records(1) if r['mode'] == 'consumer-readiness')
        self.assertEqual(('not_observed', 60, 295, 'member_count'), (readiness['outcome'], readiness['polls'], readiness['elapsed_seconds'], readiness['last_condition']))
        labels = self.labels(1)
        after = labels[labels.index(('list',)) + 60:]
        self.assertEqual([('collect', 'consumer-subject'), ('delete', 'consumer-subject'), ('confirm', 'consumer-subject'),
                          ('collect', 'consumer-witness'), ('delete', 'consumer-witness'), ('confirm', 'consumer-witness')], after)
        self.assertFalse({('render', 'start-line'), ('render', 'loadgen'), ('render', 'verify'), ('wait', 'consumer-subject')} & set(labels))
        records = {r['mode']: r for r in self.records(1)}
        for mode in ('start-line', 'loadgen', 'verify'):
            self.assertEqual(('not_attempted', 'consumer_assignment_not_observed'), (records[mode]['outcome'], records[mode]['reason']))
        for mode in MEMBER_MODES:
            member = records[mode]
            self.assertEqual((None, 0, 'not_attempted', 'consumer_assignment_not_observed'), (member['wait'], member['collect'], member['outcome'], member['reason']))

    # T11
    def test_failed_member_does_not_stop_verify(self):
        self.fake.responses[self.wait('consumer-subject', 1, 1800)] = (1, '')
        self.assertEqual(0, self.run_repeats())
        records = {r['mode']: r for r in self.records(1)}
        self.assertEqual(('failed', 'complete', 'complete'), (records['consumer-subject']['outcome'], records['consumer-witness']['outcome'], records['verify']['outcome']))
        self.assertIn(('wait', 'verify'), self.labels(1))

    # T12
    def test_unknown_member_wait_collects_deletes_and_skips_verify(self):
        self.fake.responses[self.wait('consumer-witness', 1, 1800)] = (124, '')
        self.assertEqual(1, self.run_repeats())
        labels = self.labels(1)
        index = labels.index(('wait', 'consumer-witness'))
        self.assertEqual([('wait', 'consumer-witness'), ('collect', 'consumer-witness'), ('delete', 'consumer-witness'), ('confirm', 'consumer-witness')], labels[index:index + 4])
        records = {r['mode']: r for r in self.records(1)}
        self.assertEqual('unknown', records['consumer-witness']['outcome'])
        self.assertEqual(('not_attempted', 'consumer_wait_exhausted'), (records['verify']['outcome'], records['verify']['reason']))
        self.assertNotIn(('render', 'verify'), labels)

    # Storage write check (issue 556)
    def check_job(self, attempt):
        return phase.job_name(self.run_id, 'C0', 'consumer-storage-check', attempt)

    def check_wait(self, attempt):
        return ('bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job', self.check_job(attempt), '120')

    def check_calls(self, verb):
        return [call for call in self.fake.calls if call[0][:2] == verb]

    def fail_attempts(self, count, line='consumer_storage_check result=failed cause=Azure.RequestFailedException(status=403 error_code=AuthorizationPermissionMismatch)\n'):
        for attempt in range(1, count + 1):
            self.fake.responses[self.check_wait(attempt)] = (1, '')
            self.fake.logs[self.check_job(attempt)] = line

    def access(self):
        return json.loads((self.artifacts / 'phase-status.json').read_text())['consumer_storage_access']

    def test_storage_check_succeeds_on_the_first_attempt(self):
        self.assertEqual(0, self.run_repeats())
        self.assertEqual(1, len(self.check_calls(('kubectl', 'logs'))))
        self.assertEqual({'outcome': 'ready', 'attempts': 1, 'elapsed_seconds': 0, 'last_failure': None, 'probe_delete_failed': False}, self.access())
        self.assertEqual(self.check_job(1), Path(self.check_calls(('kubectl', 'apply'))[0][0][3]).stem.removeprefix('rendered-'))
        self.assertEqual(30, len(self.records()))
        first = next(started for args, _, started in self.fake.calls if args[:2] == ('kubectl', 'apply') and 'storage-check' in args[3])
        second = next(started for args, _, started in self.fake.calls if args[:2] == ('kubectl', 'apply') and 'r1-consumer-subject' in args[3])
        self.assertEqual(first, second)  # a first-attempt success adds no 60 s sleep before the repeats

    def test_storage_check_succeeds_after_failed_attempts_with_one_sleep_between_each(self):
        self.fail_attempts(3)
        sleeps = []
        with self.mocked():
            with patch.object(phase.Phase, 'sleep', side_effect=lambda seconds: (sleeps.append(seconds), self.sleep(seconds))):
                self.assertEqual(0, self.main('run-repeats'))
        self.assertEqual(4, len(self.check_calls(('kubectl', 'logs'))))
        self.assertEqual(3, len([s for s in sleeps if s == 60]))
        starts = [started for args, _, started in self.fake.calls if args[:2] == ('kubectl', 'apply') and 'storage-check' in args[3]]
        self.assertEqual([START, START + 60, START + 120, START + 180], starts)
        self.assertEqual({'outcome': 'ready', 'attempts': 4, 'elapsed_seconds': 180, 'last_failure': None, 'probe_delete_failed': False}, self.access())
        self.assertEqual(30, len(self.records()))
        self.assertTrue(all(r['outcome'] == 'complete' for r in self.records() if r['mode'] != 'consumer-readiness'))

    def test_storage_check_exhaustion_skips_every_repeat_without_a_member_job(self):
        self.fail_attempts(40)
        self.assertEqual(1, self.run_repeats())
        access = self.access()
        self.assertEqual(('not_ready', 30, 1800), (access['outcome'], access['attempts'], access['elapsed_seconds']))
        self.assertEqual('Azure.RequestFailedException(status=403 error_code=AuthorizationPermissionMismatch)', access['last_failure'])
        applied = [Path(call[0][3]).stem for call in self.check_calls(('kubectl', 'apply'))]
        self.assertTrue(all('storage-check' in name for name in applied))
        records = self.records()
        self.assertEqual(30, len(records))
        for repeat in range(1, 6):
            self.assertEqual(SIX, [r['mode'] for r in self.records(repeat)])
            self.assertEqual({('not_attempted', 'consumer_storage_access_not_ready')}, {(r['outcome'], r['reason']) for r in self.records(repeat)})
        self.assertFalse([call for call in self.fake.calls if call[0][:4] == ('az', 'storage', 'blob', 'list')])

    def test_storage_check_time_is_not_counted_against_the_phase_clock(self):
        """Each attempt takes 60 s and is followed by a 60 s sleep: 12 failed attempts and a 13th that succeeds take 1500 s.
        Each of repeats 1 to 4 then takes 200 s. Counted from the start of the check, repeat 5 would begin at 2240 s,
        after the 2100 s admission limit (7200 - 5100), and be skipped; counted from the end of the check it begins at 800 s."""
        self.fail_attempts(12)
        checks = {self.check_job(n) for n in range(1, 14)}

        def hook(args):
            if args[:3] == ['bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job'] and args[3] in checks:
                self.now += 60
        self.fake.hooks.append(hook)
        self.repeat_takes({n: 200 for n in range(1, 5)})
        self.assertEqual(0, self.run_repeats())
        self.assertEqual((13, 1500), (self.access()['attempts'], self.access()['elapsed_seconds']))
        self.assertEqual(30, len(self.records()))
        self.assertEqual(['complete'] * 5, [r['outcome'] for r in self.records() if r['mode'] == 'verify'])

    def test_storage_check_keeps_only_the_fixed_result_grammar(self):
        for line, expected in (('consumer_storage_check result=failed cause=Azure.Identity.AuthenticationFailedException(aadsts=AADSTS70011)\n',
                                'Azure.Identity.AuthenticationFailedException(aadsts=AADSTS70011)'),
                               ('consumer_storage_check result=failed cause=System.Net.Http.HttpRequestException>System.Net.Sockets.SocketException(socket_error=HostNotFound)\n',
                                'System.Net.Http.HttpRequestException>System.Net.Sockets.SocketException(socket_error=HostNotFound)'),
                               ('consumer_storage_check result=configuration_invalid\n', 'configuration_invalid'),
                               ('consumer_storage_check result=failed cause=has"quote\n', 'wait_exit_1_no_result'),
                               ('', 'wait_exit_1_no_result')):
            with self.subTest(line=line):
                self.setUp()
                self.fail_attempts(40, line)
                self.run_repeats()
                self.assertEqual(expected, self.access()['last_failure'])

    def test_storage_check_probe_delete_failure_still_counts_as_ready(self):
        self.fake.responses[self.check_wait(1)] = (1, '')
        self.fake.logs[self.check_job(1)] = 'consumer_storage_check result=probe_delete_failed cause=Azure.RequestFailedException(status=403 error_code=AuthorizationPermissionMismatch)\n'
        self.assertEqual(0, self.run_repeats())
        self.assertEqual(('ready', True), (self.access()['outcome'], self.access()['probe_delete_failed']))

    def test_storage_check_job_that_cannot_be_deleted_sets_safety_stop(self):
        self.fake.responses[('kubectl', 'delete', 'job', self.check_job(1), '-n', 'parchwell', '--ignore-not-found',
                             '--cascade=foreground', '--wait=true', '--timeout=90s', '--request-timeout=30s')] = (1, '')
        self.assertEqual(1, self.run_repeats())
        for repeat in range(1, 6):
            self.assertEqual({('not_attempted', 'safety_stop')}, {(r['outcome'], r['reason']) for r in self.records(repeat)})

    # T16
    def test_safety_stop_before_a_repeat_runs_nothing(self):
        self.start_one_of_two_fails(False, True, repeat=3)
        self.fake.responses[('kubectl', 'delete', 'job', self.name('consumer-subject', 3), '-n', 'parchwell', '--ignore-not-found',
                             '--cascade=foreground', '--wait=true', '--timeout=90s', '--request-timeout=30s')] = (1, '')
        self.assertEqual(1, self.run_repeats())
        mark = next(i for i, call in enumerate(self.fake.calls) if call[0][:3] == ('kubectl', 'delete', 'job') and call[0][3] == self.name('consumer-witness', 3)) + 2
        self.assertEqual(mark, len(self.fake.calls))
        for repeat in (4, 5):
            records = self.records(repeat)
            self.assertEqual(SIX, [r['mode'] for r in records])
            self.assertEqual({('not_attempted', 'safety_stop')}, {(r['outcome'], r['reason']) for r in records})

    # T16c
    def test_safety_stop_during_a_repeat(self):
        self.fake.responses[self.wait('start-line', 2, 150)] = (124, '')
        self.fake.responses[('kubectl', 'delete', 'job', self.name('start-line', 2), '-n', 'parchwell', '--ignore-not-found',
                             '--cascade=foreground', '--wait=true', '--timeout=90s', '--request-timeout=30s')] = (1, '')
        self.assertEqual(1, self.run_repeats())
        records = {r['mode']: r for r in self.records(2)}
        self.assertEqual('unknown', records['start-line']['outcome'])
        for mode in ('loadgen', 'verify', 'consumer-subject', 'consumer-witness'):
            self.assertEqual(('not_attempted', 'safety_stop'), (records[mode]['outcome'], records[mode]['reason']), mode)
        for mode in MEMBER_MODES:
            self.assertEqual((None, None), (records[mode]['wait'], records[mode]['collect']))
        labels = self.labels(2)
        self.assertFalse({('render', 'loadgen'), ('render', 'verify'), ('wait', 'consumer-subject'), ('wait', 'consumer-witness'),
                          ('collect', 'consumer-subject'), ('collect', 'consumer-witness')} & set(labels))
        for repeat in (3, 4, 5):
            self.assertEqual({('not_attempted', 'safety_stop')}, {(r['outcome'], r['reason']) for r in self.records(repeat)})
            self.assertEqual([], self.labels(repeat))

    def repeat_takes(self, seconds_by_repeat):
        """Make repeat n last `seconds_by_repeat[n]` seconds from its first apply to its verifier, by advancing the clock in the load generator wait (readiness is ready at the first poll, so it takes no time)."""
        waits = {self.name('loadgen', n): total for n, total in seconds_by_repeat.items()}

        def hook(args):
            if args[:3] == ['bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job'] and args[3] in waits:
                self.now += waits[args[3]]
        self.fake.hooks.append(hook)

    # T16a
    def test_phase_budget(self):
        for label, durations, ran in (('526 s each', {n: 526 for n in range(1, 5)}, 4), ('525 s each', {n: 525 for n in range(1, 5)}, 5), ('2101 s first', {1: 2101}, 1)):
            with self.subTest(label):
                self.setUp()
                self.repeat_takes(durations)
                self.run_repeats()
                for repeat in range(1, 6):
                    records = self.records(repeat)
                    self.assertEqual(SIX, [r['mode'] for r in records])
                    if repeat <= ran:
                        self.assertEqual('complete', next(r['outcome'] for r in records if r['mode'] == 'verify'))
                    else:
                        self.assertEqual({('not_attempted', 'orchestration_deadline')}, {(r['outcome'], r['reason']) for r in records}, repeat)
                        self.assertEqual([], self.labels(repeat))

    # T16b
    def test_member_wait_is_anchored_to_the_apply(self):
        for elapsed, argument, timeout in ((2000, 1, 91), (1000, 800, 890)):
            with self.subTest(elapsed=elapsed):
                self.setUp()
                self.repeat_takes({1: elapsed})
                self.run_repeats()
                waits = [call for call in self.fake.calls if call[0][:3] == ('bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job') and call[0][3] == self.name('consumer-subject', 1)]
                self.assertEqual([(str(argument), timeout)], [(call[0][4], call[1]) for call in waits])

    # T13
    def poll(self, layout):
        self.fake.layout = lambda number: layout
        with self.mocked():
            repeats = phase.Phase().consumer_repeats()
            prefix = f'{self.run_id}/C0/r1/consumer/'
            return repeats.poll(prefix, 'poll-test')

    def test_ready_rows(self):
        self.assertEqual('ready', self.poll(ready_layout()))

    # T14
    def test_not_ready_conditions(self):
        first, second = list(range(16)), list(range(16, 32))
        cases = (('member_count', {PODS[0]: [event(first)]}),
                 ('member_count', {**ready_layout(), 'pod-c': [event([])]}),
                 ('overlap', {PODS[0]: [event(list(range(17)))], PODS[1]: [event(second)]}),
                 ('coverage', {PODS[0]: [event(first)], PODS[1]: [event(list(range(17, 32)))]}),
                 ('not_assigned', {PODS[0]: [event(first), event([], 'revoked')], PODS[1]: [event(second)]}),
                 ('not_assigned', {PODS[0]: [], PODS[1]: [event(second)]}),
                 ('row_invalid', {PODS[0]: [event(first), '[]\n'], PODS[1]: [event(second)]}))
        for condition, layout in cases:
            with self.subTest(condition=condition, pods=sorted(layout)):
                self.assertEqual(condition, self.poll(layout))

    # T15
    def test_list_failure_is_a_failed_poll(self):
        prefix = f'{self.run_id}/C0/r1/consumer/'
        self.fake.responses[('az', 'storage', 'blob', 'list', '--account-name', 'synthetic', '--container-name', 'consumer-record', '--prefix', prefix,
                             '--auth-mode', 'login', '--num-results', '1000', '--only-show-errors', '-o', 'json')] = (1, '')
        self.assertEqual('list_failed', self.poll(ready_layout()))
        self.fake.responses.clear()
        self.assertEqual('ready', self.poll(ready_layout()))

    def test_constants_match_phase(self):
        self.assertEqual(phase.BAD_OUTCOMES, set(consumer_repeat.BAD_OUTCOMES))
        self.assertEqual(phase.SLOTS, consumer_repeat.SLOTS)
        self.assertEqual(2621440000, consumer_repeat.RECORD_BLOB_MAXIMUM)


if __name__ == '__main__':
    unittest.main()

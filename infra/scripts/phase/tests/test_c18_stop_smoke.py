"""Synthetic tests of the C18 stop smoke (plan c18-stop-smoke): a scripted command runner, a fake clock and fake streams."""
import contextlib
import importlib.machinery
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import types
import unittest
from datetime import datetime, timedelta, timezone
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parent))
import test_consumer_repeat as base  # noqa: E402  (its Fake, fixtures and setUp; its test class is not imported by name)
import test_phase as shared  # noqa: E402

REPO = Path(__file__).resolve().parents[4]
phase = shared.phase
SPEC = importlib.util.spec_from_file_location('c18_stop_smoke', REPO / 'infra/scripts/phase/c18_stop_smoke.py')
c18 = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(c18)

START = base.START
RUN = 'smoke1'
ORIGIN = datetime(2026, 11, 28, 10, 0, 0, tzinfo=timezone.utc)
ISSUED = '2026-11-28T10:00:00.123Z'
DEADLINE = '2026-11-28T10:00:30Z'
READY = {base.PODS[0]: [base.event(list(range(32)))]}
FAILURE_LINE = 'consumer_processing_failed reason=consumer_fatal_error scenario_id=C03 run_id=smoke1 role=subject cause=System.OperationCanceledException'
KEYS = ['end_time', 'end_time_source', 'delete', 'job', 'job_conditions', 'job_status', 'log', 'node', 'outcome', 'pod', 'pod_uid', 'pods_seen',
        'poll', 'readiness', 'reason', 'replacement_pod', 'sigterm', 'trial', 'watch']
TOP_KEYS = ['bounds', 'configuration', 'consumer_storage_access', 'dialect', 'grace_seconds', 'run_id', 'scenario', 'schema_version',
            'source_commit', 'trials']


def pod_object(name, terminated=None, deletion=None, grace=30, extra_status=None):
    """A pod as `kubectl get pod -o json` prints it. terminated is (exit code, finishedAt)."""
    state = ({'terminated': {'exitCode': terminated[0], 'finishedAt': terminated[1], 'startedAt': '2026-11-28T09:59:00Z', 'reason': 'Error'}}
             if terminated else {'running': {'startedAt': '2026-11-28T09:59:00Z'}})
    metadata = {'name': name, 'uid': 'uid-1'}
    if deletion:
        metadata.update(deletionTimestamp=deletion, deletionGracePeriodSeconds=grace)
    return {'metadata': metadata, 'spec': {'nodeName': 'node-1'},
            'status': {'phase': 'Running', 'containerStatuses': [{'name': 'enrichment-consumer', 'state': state}], **(extra_status or {})}}


def serialized(item):
    return json.dumps(item)


class FakeWatch:
    def __init__(self, test, events=(), started=True):
        self.test, self.events, self.started = test, list(events), started

    def stop(self):
        self.test.fake.calls.append((('STOP', 'watch'), 0, self.test.now))


class FakeLog:
    def __init__(self, test, path, started=True):
        self.test, self.path, self.started = test, path, started

    def stop(self):
        self.test.fake.calls.append((('STOP', 'log'), 0, self.test.now))


class SmokeFake(base.Fake):
    """The consumer fake plus the pod, Job and listing commands of the smoke."""
    def __init__(self, test):
        super().__init__(test)
        self.layout = lambda poll: dict(READY)
        self.pod_polls = {}  # pod name -> list of (exit code, output); an exhausted list answers NotFound
        self.poll_costs = []  # seconds each pod poll takes, in order
        self.delete_code = 0
        self.delete_seconds = 1
        self.confirm = {}  # job -> output of the confirmation listing
        self.extra_pods = lambda job, number, not_found: []
        self.listings = {}
        self.not_found = False

    def default_polls(self):
        return [(0, serialized(pod_object('p', None))), (0, serialized(pod_object('p', (1, '2026-11-28T10:00:02Z'), DEADLINE))), (0, '')]

    def __call__(self, instance, args, seconds, output=None, maximum=4 * 1024 * 1024):
        args = list(map(str, args))
        test = self.test
        if args[:3] == ['kubectl', 'get', 'pod'] and '--ignore-not-found' in args:
            self.calls.append((tuple(args), seconds, test.now))
            queue = self.pod_polls.setdefault(args[3], self.default_polls_for(args[3]))
            code, content = queue.pop(0) if queue else (0, '')
            test.now += self.poll_costs.pop(0) if self.poll_costs else 0
            self.not_found = code == 0 and not content
            Path(output).write_bytes(content.encode())
            return code
        if args[:3] == ['kubectl', 'delete', 'pod']:
            self.calls.append((tuple(args), seconds, test.now))
            test.now += self.delete_seconds
            self.not_found = False
            for hook in self.hooks:
                hook(args)
            return self.delete_code
        if args[:3] == ['kubectl', 'get', 'pods'] and Path(output).name.startswith('confirm-'):
            job = args[6].removeprefix('job-name=')
            self.calls.append((tuple(args), seconds, test.now))
            Path(output).write_bytes(self.confirm.get(job, '{"items": []}').encode())
            return 0
        if args[:3] == ['kubectl', 'get', 'pods']:
            job = args[6].removeprefix('job-name=')
            self.calls.append((tuple(args), seconds, test.now))
            number = self.listings[job] = self.listings.get(job, 0) + 1
            items = [pod_object('pod-' + job)] + [pod_object(name) for name in self.extra_pods(job, number, self.not_found)]
            Path(output).write_bytes(json.dumps({'items': items}).encode())
            return 0
        if args[:3] == ['kubectl', 'get', 'job']:
            self.calls.append((tuple(args), seconds, test.now))
            status = {'active': 0, 'failed': 1, 'conditions': [{'type': 'Failed', 'status': 'True', 'reason': 'BackoffLimitExceeded', 'message': 'x'}]}
            Path(output).write_bytes(json.dumps({'status': status}).encode())
            return 0
        return super().__call__(instance, args, seconds, output, maximum)

    def default_polls_for(self, pod):
        return [(code, content.replace('"p"', json.dumps(pod)) if content else content) for code, content in self.default_polls()]


class SmokeTestCase(unittest.TestCase):
    def setUp(self):
        base.ConsumerRepeatTests.setUp(self)  # the consumer fixtures: a prepared C03 phase with private and artifact directories
        state = json.loads((self.private / 'phase.json').read_text())
        state['run_id'] = RUN
        (self.private / 'phase.json').write_text(json.dumps(state))
        self.run_id = RUN
        self.fake = SmokeFake(self)
        self.fake.hooks.append(lambda args: setattr(self.fake, 'not_found', False) if args[:2] == ['kubectl', 'apply'] else None)
        self.mark = START + 30  # the fake clock time that is 10:00:00 UTC; each trial sets it when its streams start
        self.watch_events = []
        self.watch_started = True
        self.log_started = True
        self.smoke = c18.Smoke(phase.Phase(), phase.Failure)

    def main(self, *argv):
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return phase.main(list(argv))

    def sleep(self, seconds):
        self.now += seconds

    def utc(self):
        return ORIGIN + timedelta(seconds=self.now - self.mark)

    def start_watch(self, instance, args):
        self.fake.calls.append((('START', 'watch', *args), 0, self.now))
        self.mark = self.now + 30  # the member is ready now, so the delete follows 30 s later
        self.fake.not_found = False
        events = [{'type': kind, 'object': item, 'received_at': ORIGIN + timedelta(seconds=offset)} for kind, item, offset in self.watch_events]
        return FakeWatch(self, events, self.watch_started)

    def start_log(self, instance, args, path):
        self.fake.calls.append((('START', 'log', *args), 0, self.now))
        return FakeLog(self, path, self.log_started)

    def mocked(self):
        stack = contextlib.ExitStack()
        stack.enter_context(patch.object(phase.Phase, 'run', autospec=True, side_effect=self.fake))
        stack.enter_context(patch.object(phase.Phase, 'sleep', side_effect=self.sleep))
        stack.enter_context(patch.object(time, 'monotonic', side_effect=lambda: self.now))
        stack.enter_context(patch.object(c18, 'utc', side_effect=self.utc))
        stack.enter_context(patch.object(c18.Smoke, 'start_watch', autospec=True, side_effect=self.start_watch))
        stack.enter_context(patch.object(c18.Smoke, 'start_log', autospec=True, side_effect=self.start_log))
        return stack

    def run_smoke(self):
        with self.mocked():
            return self.smoke.run()

    def saved(self):
        return json.loads((self.artifacts / 'phase-status.json').read_text())

    def trials(self):
        return self.saved()['c18_stop_smoke']['trials']

    def job(self, number):
        return phase.job_name(RUN, 'C0', 'consumer-subject', number)

    def calls(self, *verb):
        return [call for call in self.fake.calls if call[0][:len(verb)] == verb]

    def delete_job_command(self, job):
        return ('kubectl', 'delete', 'job', job, '-n', 'parchwell', '--ignore-not-found', '--cascade=foreground', '--wait=true',
                '--timeout=90s', '--request-timeout=30s')

    def check_job(self, attempt):
        return phase.job_name(RUN, 'C0', 'consumer-storage-check', attempt)


class StorageTests(SmokeTestCase):
    FAILED = 'consumer_storage_check result=failed cause=Azure.RequestFailedException(status=403)\n'

    def fail_attempts(self, count=40):
        for attempt in range(1, count + 1):
            self.fake.responses[('bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job', self.check_job(attempt), '120')] = (1, '')
            self.fake.logs[self.check_job(attempt)] = self.FAILED

    def test_not_ready_skips_all_trials(self):
        self.fail_attempts()
        self.assertEqual(1, self.run_smoke())
        trials = self.trials()
        self.assertEqual([1, 2, 3], [trial['trial'] for trial in trials])
        for number, trial in enumerate(trials, 1):
            self.assertEqual({'trial': number, 'job': self.job(number), 'outcome': 'not_attempted', 'reason': 'consumer_storage_access_not_ready'},
                             {key: trial[key] for key in ('trial', 'job', 'outcome', 'reason')})
            self.assertEqual(sorted(KEYS), sorted(trial))
            self.assertEqual([], trial['pods_seen'] + trial['job_conditions'])
            self.assertTrue(all(trial[key] is None for key in KEYS if key not in ('trial', 'job', 'outcome', 'reason', 'pods_seen', 'job_conditions')))
        self.assertEqual('not_ready', self.saved()['consumer_storage_access']['outcome'])
        self.assertFalse([call for call in self.calls('kubectl', 'apply') if 'consumer-subject' in call[0][3]])

    def test_not_ready_with_a_failed_check_delete_is_a_safety_stop(self):
        self.fail_attempts()
        self.fake.responses[self.delete_job_command(self.check_job(1))] = (1, '')
        self.assertEqual(1, self.run_smoke())
        self.assertEqual({('not_attempted', 'safety_stop')}, {(trial['outcome'], trial['reason']) for trial in self.trials()})
        self.assertFalse([call for call in self.calls('kubectl', 'apply') if 'consumer-subject' in call[0][3]])


class TrialTests(SmokeTestCase):
    def test_one_subject_job_per_trial(self):
        self.assertEqual(0, self.run_smoke())
        applied = [Path(call[0][3]).stem.removeprefix('rendered-') for call in self.calls('kubectl', 'apply') if 'consumer-subject' in call[0][3]]
        self.assertEqual(['smoke1-c0-r1-consumer-subject', 'smoke1-c0-r2-consumer-subject', 'smoke1-c0-r3-consumer-subject'], applied)
        trials = self.trials()
        self.assertEqual(['observed'] * 3, [trial['outcome'] for trial in trials])
        for trial in trials:
            self.assertEqual(sorted(KEYS), sorted(trial))
            self.assertEqual('reached_worker', trial['sigterm'])
        first = trials[0]
        self.assertEqual(('pod-' + self.job(1), 'uid-1', 'node-1'), (first['pod'], first['pod_uid'], first['node']))
        self.assertEqual({'outcome': 'ready', 'polls': 1, 'elapsed_seconds': 0, 'last_condition': 'ready'}, first['readiness'])
        self.assertEqual([{'type': 'Failed', 'status': 'True', 'reason': 'BackoffLimitExceeded'}], first['job_conditions'])
        self.assertEqual({'active': 0, 'failed': 1, 'terminating': None}, first['job_status'])
        self.assertFalse(first['replacement_pod'])

    def test_readiness_timeout(self):
        self.fake.layout = lambda poll: {}
        self.assertEqual(1, self.run_smoke())
        for number, trial in enumerate(self.trials(), 1):
            self.assertEqual(('not_attempted', 'readiness_not_observed'), (trial['outcome'], trial['reason']))
            self.assertEqual(('not_observed', 'member_count'), (trial['readiness']['outcome'], trial['readiness']['last_condition']))
            self.assertEqual(self.job(number), trial['job'])
            self.assertEqual(1, len([call for call in self.calls('kubectl', 'delete', 'job') if call[0][3] == self.job(number)]))
        self.assertEqual([], self.calls('kubectl', 'delete', 'pod'))

    def test_readiness_requires_all_32(self):
        self.fake.layout = lambda poll: {base.PODS[0]: [base.event(list(range(31)))]}
        self.assertEqual(1, self.run_smoke())
        self.assertEqual({('not_attempted', 'readiness_not_observed', 'coverage')},
                         {(t['outcome'], t['reason'], t['readiness']['last_condition']) for t in self.trials()})
        self.fake.layout = lambda poll: {base.PODS[0]: [base.event(list(range(32)))]}
        self.assertEqual(0, self.run_smoke())
        self.assertEqual({'observed'}, {t['outcome'] for t in self.trials()})

    def test_delete_after_delay_with_streams_started(self):
        self.fake.layout = lambda poll: {} if poll < 5 else dict(READY)  # the fifth poll, at t=20 s, is the first that is ready
        self.assertEqual(0, self.run_smoke())
        calls = self.fake.calls
        first = next(index for index, call in enumerate(calls) if call[0][:3] == ('kubectl', 'delete', 'pod'))
        pod = 'pod-' + self.job(1)
        self.assertEqual(('kubectl', 'delete', 'pod', pod, '-n', 'parchwell', '--wait=false', '--request-timeout=30s'), calls[first][0])
        self.assertEqual(START + 50, calls[first][2])
        starts = {}
        for index, call in enumerate(calls):
            if call[0][0] == 'START':
                starts.setdefault(call[0][1], index)
        self.assertLess(starts['watch'], first)
        self.assertLess(starts['log'], first)
        self.assertEqual(('START', 'watch', 'kubectl', 'get', 'pod', pod, '-n', 'parchwell', '--watch', '--output-watch-events', '-o', 'json',
                          '--request-timeout=0'), calls[starts['watch']][0])
        self.assertEqual(('START', 'log', 'kubectl', 'logs', '-f', '-n', 'parchwell', pod, '-c', 'enrichment-consumer', '--limit-bytes=1048576',
                          '--request-timeout=0'), calls[starts['log']][0])
        self.assertEqual({'outcome': 'ready', 'polls': 5, 'elapsed_seconds': 20, 'last_condition': 'ready'}, self.trials()[0]['readiness'])

    def test_delete_failure(self):
        self.fake.delete_code = 1
        self.assertEqual(1, self.run_smoke())
        for number, trial in enumerate(self.trials(), 1):
            self.assertEqual(('not_attempted', 'delete_failed', 1), (trial['outcome'], trial['reason'], trial['delete']['exit_code']))
            self.assertEqual(1, len([call for call in self.calls('kubectl', 'delete', 'job') if call[0][3] == self.job(number)]))

    def test_safety_stop(self):
        self.fake.confirm[self.job(1)] = json.dumps({'items': [{'metadata': {'name': 'left-over'}}]})
        self.assertEqual(1, self.run_smoke())
        trials = self.trials()
        self.assertEqual('observed', trials[0]['outcome'])
        self.assertEqual([('not_attempted', 'safety_stop')] * 2, [(t['outcome'], t['reason']) for t in trials[1:]])
        applied = [Path(call[0][3]).stem.removeprefix('rendered-') for call in self.calls('kubectl', 'apply') if 'consumer-subject' in call[0][3]]
        self.assertEqual([self.job(1)], applied)

    def test_malformed_watch_falls_back_to_poll(self):
        terminated = serialized(pod_object('p', (1, '2026-11-28T10:00:02Z'), DEADLINE))
        running = serialized(pod_object('p'))
        for number in (1, 2, 3):
            pod = 'pod-' + self.job(number)
            self.fake.pod_polls[pod] = [(0, running.replace('"p"', json.dumps(pod))), (0, running.replace('"p"', json.dumps(pod))),
                                        (0, terminated.replace('"p"', json.dumps(pod))), (0, '')]
        self.assertEqual(0, self.run_smoke())
        first = self.trials()[0]
        self.assertEqual(('observed', True, None, 'poll', 'reached_worker'),
                         (first['outcome'], first['watch']['started'], first['watch']['terminated'], first['end_time_source'], first['sigterm']))
        self.assertEqual(0, first['watch']['events'])

    def test_watch_and_log_not_started(self):
        self.watch_started = self.log_started = False
        self.assertEqual(0, self.run_smoke())
        first = self.trials()[0]
        self.assertEqual((False, False, None, 'poll'), (first['watch']['started'], first['log']['stream_started'],
                                                         first['log']['failure_line_reason'], first['end_time_source']))

    def test_poll_longest_seconds(self):
        self.fake.poll_costs = [0.2, 0.7, 0.4]
        self.assertEqual(0, self.run_smoke())
        poll = self.trials()[0]['poll']
        self.assertEqual((3, 0.7), (poll['polls'], poll['longest_seconds']))

    def test_poll_error_is_not_not_found(self):
        terminated = serialized(pod_object('p', (1, '2026-11-28T10:00:02Z'), DEADLINE))
        for number in (1, 2, 3):
            pod = 'pod-' + self.job(number)
            self.fake.pod_polls[pod] = [(1, ''), (1, ''), (0, terminated.replace('"p"', json.dumps(pod))), (0, '')]
        self.assertEqual(0, self.run_smoke())
        poll = self.trials()[0]['poll']
        self.assertEqual(4, poll['polls'])
        self.assertEqual('2026-11-28T10:00:03.000Z', poll['terminated_read_at'])
        self.assertEqual('2026-11-28T10:00:04.000Z', poll['not_found_at'])
        self.assertEqual((1, '2026-11-28T10:00:02Z'), (poll['terminated']['exit_code'], poll['terminated']['finished_at']))

    def test_observation_stops_at_120_seconds_after_the_delete(self):
        for number in (1, 2, 3):
            self.fake.pod_polls['pod-' + self.job(number)] = [(0, serialized(pod_object('pod-' + self.job(number))))] * 500
        self.assertEqual(1, self.run_smoke())
        trial = self.trials()[0]
        self.assertEqual(120, trial['poll']['polls'])  # polls from 1 s after the delete (the delete takes 1 s) to 120 s after it, one second apart
        self.assertIsNone(trial['poll']['not_found_at'])
        self.assertEqual(('observed', 'undetermined', 'not_read', 'none'), (trial['outcome'], trial['sigterm'], trial['end_time'], trial['end_time_source']))

    def test_watch_events_fill_the_labels_and_the_deadline(self):
        pod = 'pod-' + self.job(1)
        self.watch_events = [('MODIFIED', pod_object(pod, None, '2026-11-28T10:00:30Z'), 0.5),
                             ('MODIFIED', pod_object(pod, (143, '2026-11-28T10:00:01Z'), '2026-11-28T10:00:30Z'), 1.5),
                             ('DELETED', pod_object(pod, (143, '2026-11-28T10:00:01Z'), '2026-11-28T10:00:30Z'), 2.5)]
        self.assertEqual(0, self.run_smoke())
        first = self.trials()[0]
        self.assertEqual({'exit_code': 143, 'signal': None, 'reason': 'Error', 'started_at': '2026-11-28T09:59:00Z', 'finished_at': '2026-11-28T10:00:01Z'},
                         first['watch']['terminated'])
        self.assertEqual((3, 'MODIFIED', '2026-11-28T10:00:01.500Z', '2026-11-28T10:00:02.500Z'),
                         (first['watch']['events'], first['watch']['terminated_event_type'], first['watch']['terminated_received_at'],
                          first['watch']['deleted_received_at']))
        self.assertEqual(('both', 'read', 'reached_worker'), (first['end_time_source'], first['end_time'], first['sigterm']))
        self.assertEqual(('2026-11-28T10:00:30Z', 30), (first['delete']['deletion_timestamp'], first['delete']['deletion_grace_period_seconds']))

    def test_failure_line_comes_from_the_log_file(self):
        pod = 'pod-' + self.job(1)
        self.fake.pod_polls[pod] = [(0, serialized(pod_object(pod, (137, '2026-11-28T10:00:31Z'), DEADLINE))), (0, '')]
        self.fake.hooks.append(lambda args: (self.private / f'{self.job(1)}-stream.log').write_text(FAILURE_LINE + '\nsecret-text\n')
                               if args[:3] == ['kubectl', 'delete', 'pod'] and args[3] == pod else None)
        self.assertEqual(0, self.run_smoke())
        first = self.trials()[0]
        self.assertEqual(('consumer_fatal_error', 'reached_but_killed_at_grace'), (first['log']['failure_line_reason'], first['sigterm']))

    def test_replacement_pod(self):
        self.assertEqual(0, self.run_smoke())
        self.assertEqual([False] * 3, [trial['replacement_pod'] for trial in self.trials()])
        self.fake.extra_pods = lambda job, number, not_found: ['replacement'] if number >= 2 else []
        self.fake.listings.clear()
        self.fake.pod_polls.clear()
        self.assertEqual(0, self.run_smoke())
        self.assertEqual([True] * 3, [trial['replacement_pod'] for trial in self.trials()])
        self.assertEqual(['pod-' + self.job(1), 'replacement'], self.trials()[0]['pods_seen'])

    def test_replacement_pod_seen_only_after_not_found(self):
        self.fake.extra_pods = lambda job, number, not_found: ['replacement'] if not_found else []
        self.assertEqual(0, self.run_smoke())
        self.assertEqual([True] * 3, [trial['replacement_pod'] for trial in self.trials()])


class ClassifyTests(unittest.TestCase):
    def terminated(self, code, finished):
        return {'exit_code': code, 'signal': None, 'reason': 'Error', 'started_at': None, 'finished_at': finished}

    def label(self, code, finished, deadline=DEADLINE, issued=ISSUED, reason=None):
        return c18.classify(self.terminated(code, finished), issued, deadline, reason)

    def test_rules(self):
        self.assertEqual('reached_worker', self.label(1, '2026-11-28T10:00:02Z'))  # 1
        self.assertEqual('reached_worker', self.label(143, '2026-11-28T10:00:01Z'))  # 2
        self.assertEqual('undetermined', self.label(1, '2026-11-28T10:00:30Z'))  # 3: not earlier than the deadline
        self.assertEqual('reached_but_killed_at_grace', self.label(137, '2026-11-28T10:00:30Z', reason='consumer_fatal_error'))  # 4
        self.assertEqual('not_reached', self.label(137, '2026-11-28T10:00:30Z'))  # 5
        self.assertEqual('undetermined', c18.classify(None, ISSUED, DEADLINE, None))  # 6
        self.assertEqual('undetermined', self.label(1, '2026-11-28T09:59:59Z'))  # 10: ended before the delete
        self.assertEqual('reached_worker', self.label(1, '2026-11-28T10:00:00Z'))  # 11: the delete time cut to whole seconds
        self.assertEqual('undetermined', self.label(1, '2026-11-28T10:00:02Z', deadline=None))  # 12: a null deadline only stops reached_worker
        self.assertEqual('not_reached', self.label(137, '2026-11-28T10:00:02Z', deadline=None))  # 13
        self.assertEqual('reached_but_killed_at_grace', self.label(137, '2026-11-28T10:00:02Z', deadline=None, reason='consumer_fatal_error'))  # 14

    def test_end_time_sources(self):
        read = self.terminated(1, '2026-11-28T10:00:02Z')
        self.assertEqual(('not_read', 'none'), c18.end_time(None, None))  # 6
        self.assertEqual(('read', 'watch'), c18.end_time(read, None))  # 7
        self.assertEqual(('read', 'poll'), c18.end_time(None, read))
        self.assertEqual(('read', 'both'), c18.end_time(read, read))
        self.assertEqual(('read', 'both'), c18.end_time(read, self.terminated(1, '2026-11-28T10:00:03Z')))

    def test_first_deletion_wins(self):
        first = datetime(2026, 11, 28, 10, 0, 1, tzinfo=timezone.utc)
        objects = [(first + timedelta(seconds=1), pod_object('p', None, '2026-11-28T10:00:05Z', 0)),
                   (first, pod_object('p', None, DEADLINE, 30)),
                   (first - timedelta(seconds=1), pod_object('p'))]
        self.assertEqual((DEADLINE, 30), c18.first_deletion(objects))  # 9: the object received first, whatever the list order
        self.assertEqual((None, None), c18.first_deletion([(first, pod_object('p'))]))

    def test_terminated_state_reads_only_the_consumer_container(self):
        other = pod_object('p', (1, '2026-11-28T10:00:02Z'))
        other['status']['containerStatuses'][0]['name'] = 'sidecar'
        self.assertIsNone(c18.terminated_state(other))
        self.assertEqual({'exit_code': 2, 'signal': None, 'reason': 'Error', 'started_at': '2026-11-28T09:59:00Z', 'finished_at': 'x'},
                         c18.terminated_state(pod_object('p', (2, 'x'))))
        self.assertIsNone(c18.terminated_state({'status': {}}))


class ExitTests(unittest.TestCase):
    def trial(self, outcome='observed', sigterm='reached_worker'):
        return {'outcome': outcome, 'sigterm': sigterm}

    def test_exit_status(self):
        good = self.trial()
        self.assertEqual(0, c18.exit_status([good] * 3))
        self.assertEqual(0, c18.exit_status([good, good, self.trial(sigterm='not_reached')]))
        self.assertEqual(0, c18.exit_status([good, good, self.trial(sigterm='reached_but_killed_at_grace')]))
        self.assertEqual(0, c18.exit_status([good, good, {**good, 'replacement_pod': True}]))  # a replacement pod is a finding, not a failure
        self.assertEqual(1, c18.exit_status([good, good, self.trial(sigterm='undetermined')]))  # not_read gives undetermined
        self.assertEqual(1, c18.exit_status([good, good, {**self.trial(sigterm='undetermined'), 'end_time': 'read'}]))  # T present, ended at the deadline
        self.assertEqual(1, c18.exit_status([good, good, self.trial('not_attempted', None)]))


class CommandTests(SmokeTestCase):
    def test_main_dispatch(self):
        class Loader:
            def create_module(self, spec):
                return None

            def exec_module(self, module):
                module.Smoke = lambda p, f: types.SimpleNamespace(run=lambda: 1)

        spec = importlib.machinery.ModuleSpec('parchwell_c18_stop_smoke', Loader())
        with patch.object(importlib.util, 'spec_from_file_location', return_value=spec):
            self.assertEqual(1, self.main('run-c18-stop-smoke'))
            self.assertEqual(2, self.main('run-c18-stop-smoke', 'x'))

    def test_prepare_shared_arguments(self):
        with patch.object(phase.Phase, 'prepare_shared', autospec=True, return_value=0) as prepared:
            self.assertEqual(0, self.main('prepare-c18-stop-smoke'))
        self.assertEqual(('C03', 'C0', 'C0', 'c18s'), prepared.call_args.args[1:5])


class CollectTests(SmokeTestCase):
    def document(self):
        return json.loads((self.artifacts / 'c18-stop-smoke' / 'c18-stop-smoke.json').read_text())

    def test_collect_without_run_state(self):
        self.assertEqual(0, self.smoke.collect())
        document = self.document()
        self.assertEqual(TOP_KEYS, sorted(document))
        self.assertIsNone(document['consumer_storage_access'])
        self.assertEqual(('c18_stop_smoke', 1, 'C03', 'C0', 30), (document['dialect'], document['schema_version'], document['scenario'],
                                                                   document['configuration'], document['grace_seconds']))
        self.assertEqual({'trials': 3, 'readiness_cap_seconds': 300, 'readiness_interval_seconds': 5, 'delay_after_ready_seconds': 30,
                          'observation_cap_seconds': 120, 'poll_interval_seconds': 1}, document['bounds'])
        for number, trial in enumerate(document['trials'], 1):
            self.assertEqual(sorted(KEYS), sorted(trial))
            self.assertEqual((number, 'not_attempted', 'run_not_started', self.job(number)),
                             (trial['trial'], trial['outcome'], trial['reason'], trial['job']))

    def test_collect_after_failure_and_keys(self):
        self.assertEqual(0, self.run_smoke())  # all three trials, then keep only the first, as after a failed run
        state = self.saved()
        state['c18_stop_smoke']['trials'] = state['c18_stop_smoke']['trials'][:1]
        state['c18_stop_smoke']['trials'][0]['extra_key'] = 'secret-text'
        state['c18_stop_smoke']['trials'][0]['log']['failure_line_reason'] = None
        (self.artifacts / 'phase-status.json').write_text(json.dumps(state))
        (self.private / f'{self.job(1)}-stream.log').write_text(FAILURE_LINE + '\nsecret-text\n')
        self.assertEqual(0, self.smoke.collect())
        document = self.document()
        self.assertEqual(TOP_KEYS, sorted(document))
        self.assertEqual({'outcome': 'ready', 'attempts': 1, 'elapsed_seconds': 0, 'last_failure': None, 'probe_delete_failed': False},
                         document['consumer_storage_access'])
        first, second, third = document['trials']
        self.assertEqual(sorted(KEYS), sorted(first))
        self.assertEqual('consumer_fatal_error', first['log']['failure_line_reason'])
        self.assertNotIn('secret-text', (self.artifacts / 'c18-stop-smoke' / 'c18-stop-smoke.json').read_text())
        self.assertEqual([('not_attempted', 'run_not_started')] * 2, [(t['outcome'], t['reason']) for t in (second, third)])

    def test_collect_with_a_storage_check_but_no_saved_trial_writes_null_access(self):
        state = self.saved()
        state['consumer_storage_access'] = {'outcome': 'ready', 'attempts': 1, 'elapsed_seconds': 0, 'last_failure': None, 'probe_delete_failed': False}
        (self.artifacts / 'phase-status.json').write_text(json.dumps(state))
        self.assertEqual(0, self.smoke.collect())
        self.assertIsNone(self.document()['consumer_storage_access'])

    def test_collect_fails_only_when_it_cannot_write(self):
        (self.artifacts / 'c18-stop-smoke').write_text('a file where the directory belongs')
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(1, self.smoke.collect())


class StreamTests(unittest.TestCase):
    """The real background readers, against a Python stub in place of kubectl."""
    def watch(self, script):
        stream = c18.Watch([sys.executable, '-c', script], None)
        self.assertTrue(stream.started)
        stream.process.wait(timeout=20)
        stream.thread.join(timeout=20)
        stream.stop()
        return stream

    def test_events_are_parsed_from_pretty_printed_output_split_across_writes(self):
        script = ('import json, sys, time\n'
                  'text = json.dumps({"type": "ADDED", "object": {"metadata": {"name": "p"}}}, indent=4)\n'
                  'sys.stdout.write(text[:20]); sys.stdout.flush(); time.sleep(0.2); sys.stdout.write(text[20:] + "\\n")\n'
                  'sys.stdout.write(json.dumps({"type": "DELETED", "object": {"metadata": {"name": "p"}}}, indent=4) + "\\n")\n')
        stream = self.watch(script)
        self.assertEqual(['ADDED', 'DELETED'], [event['type'] for event in stream.events])
        self.assertFalse(stream.malformed)
        self.assertTrue(all(event['received_at'].tzinfo is not None for event in stream.events))

    def test_output_that_is_not_json_is_malformed(self):
        stream = self.watch('print("not-json")')
        self.assertEqual(([], True), (stream.events, stream.malformed))

    def test_an_object_without_type_and_object_is_malformed(self):
        stream = self.watch('print(\'{"kind": "Pod"}\')')
        self.assertEqual(([], True), (stream.events, stream.malformed))

    def test_an_incomplete_object_at_the_end_is_malformed(self):
        stream = self.watch('print(\'{"type": "ADDED", "object": {\')')
        self.assertEqual(([], True), (stream.events, stream.malformed))

    def test_a_command_that_exits_one_at_start_has_not_started(self):
        stream = self.watch('import sys; sys.exit(1)')
        self.assertEqual(([], False), (stream.events, stream.started))
        with tempfile.TemporaryDirectory() as temporary:
            log = c18.LogStream([sys.executable, '-c', 'import sys; sys.exit(1)'], Path(temporary) / 'log', None)
            log.process.wait(timeout=20)
            log.stop()
            log.stop()  # stopping twice keeps the answer
            self.assertFalse(log.started)
            ok = c18.LogStream([sys.executable, '-c', 'print("x")'], Path(temporary) / 'ok', None)
            ok.process.wait(timeout=20)
            ok.stop()
            self.assertTrue(ok.started)

    def test_a_watch_that_printed_events_before_it_exited_one_has_started(self):
        stream = self.watch('import json, sys; print(json.dumps({"type": "ADDED", "object": {}})); sys.exit(1)')
        self.assertEqual((1, True), (len(stream.events), stream.started))
        stream.stop()
        self.assertTrue(stream.started)

    def test_a_command_that_cannot_start(self):
        self.assertFalse(c18.Watch(['/nonexistent/kubectl-for-tests'], None).started)
        with tempfile.TemporaryDirectory() as temporary:
            self.assertFalse(c18.LogStream(['/nonexistent/kubectl-for-tests'], Path(temporary) / 'log', None).started)

    def test_log_stream_writes_a_file_and_stops_a_running_command(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'log'
            stream = c18.LogStream([sys.executable, '-u', '-c', 'import time; print("line"); time.sleep(60)'], path, None)
            self.assertTrue(stream.started)
            deadline = time.monotonic() + 20
            while time.monotonic() < deadline and not path.read_text():
                time.sleep(0.05)
            stream.stop()
            self.assertEqual('line\n', path.read_text())
            self.assertIsNotNone(stream.process.poll())

    def test_failure_reason_reads_only_the_prefix_group(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'log'
            path.write_text('other line\n' + FAILURE_LINE + '\nconsumer_processing_failed reason=second \n')
            self.assertEqual('consumer_fatal_error', c18.Smoke.failure_reason(path))
            path.write_text('consumer_processing_failed reason=Bad-Reason x\nconsumer_processing_failed reason=ok\n')
            self.assertIsNone(c18.Smoke.failure_reason(path))
            self.assertIsNone(c18.Smoke.failure_reason(Path(temporary) / 'missing'))


if __name__ == '__main__':
    unittest.main()

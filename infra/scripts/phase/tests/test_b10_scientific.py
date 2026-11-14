"""Synthetic three-Job phase evidence; no network or Azure credentials are used."""
import copy
import json
import os
import unittest
from unittest.mock import patch
import yaml

try:
    from . import test_phase as support
    from . import test_b10_abrupt as trial_support
except ImportError:
    import test_phase as support
    import test_b10_abrupt as trial_support

phase = support.phase
abrupt = trial_support.abrupt


def prepare_scientific(owner, selected='C1'):
    os.environ['PARCHWELL_SCENARIO'] = 'B10'
    owner.prepare(selected)
    owner.p = phase.Phase()
    owner.failures = {}
    owner.metadata_mutator = None
    owner.first_mutator = None
    owner.successor_mutator = None
    owner.pod_mutator = None
    owner.configmaps = []
    owner.pod_reads = []
    owner.publications = {}
    document = owner.p.read('phase.json')
    for repeat in phase.SLOTS:
        first_job = owner.p.mode_job(document, 'loadgen', repeat)
        identity = dict(scenario_id='B10', configuration_id='C1', run_id=owner.run_id,
                        instance_id=first_job, expected_instance_id=first_job, client_id=first_job)
        prefix = owner.run_id + '/C1/r' + str(repeat) + '/'
        files = {'start-line.json': abrupt.encode(dict(identity=identity, verifier_commit=support.SHA,
            expected_job_names=[owner.p.mode_job(document, mode, repeat) for mode in
                                ('loadgen', 'loadgen-restart', 'loadgen-successor')])),
                 'broker-inventory.json': abrupt.encode(dict(identity=identity, source_commit=support.SHA,
                                                              source='kafka_metadata'))}
        files['start-line-completion.json'] = abrupt.encode(dict(schema_version=1, mode='start-line', scenario_id='B10',
            configuration_id='C1', run_id=owner.run_id, phase='C1', repeat=repeat, source_commit=support.SHA,
            job_name=owner.p.mode_job(document, 'start-line', repeat), handler_exit_code=0, cleanup_completed=True,
            artifacts=[dict(container='verifier', **abrupt.descriptor(prefix + name, raw)) for name, raw in files.items()]))
        for name, raw in files.items():
            owner.fake.blobs[prefix + name] = raw
        owner.fake.blobs[prefix + 'b10-abrupt/nonfinal-result.json'] = b'{"synthetic":true}'
        if selected == 'C1':
            owner.publications[repeat] = first_publication(owner, repeat)
    original = owner.fake

    def command(instance, args, seconds, output=None, maximum=4 * phase.MIB):
        args = list(map(str, args))
        job = None
        if args[:2] in (['kubectl', 'apply'], ['helm', 'template']):
            path = args[-1] if args[0] == 'helm' else args[3]
            job = path.rsplit('/', 1)[-1].removeprefix('values-').removeprefix('rendered-').rsplit('.', 1)[0]
        elif 'wait-job' in args:
            job = args[3]
        if job:
            for (repeat, mode, step), code in owner.failures.items():
                if job == owner.name(mode, repeat) and step == ('render' if args[0] == 'helm' else 'wait' if 'wait-job' in args else 'apply'):
                    original.responses[tuple(args)] = (code, '')
        if args[:3] == ['kubectl', 'create', '-f']:
            owner.configmaps.append(json.loads(open(args[3]).read())['metadata']['name'])
        if args[:3] == ['kubectl', 'get', 'pods'] and output is not None:
            job = args[args.index('-l') + 1].removeprefix('job-name=')
            values = json.loads((instance.private / ('values-' + job + '.json')).read_text())['runtime']
            owner.pod_reads.append(job)
            pod = scientific_pod(owner, job, values)
            if owner.pod_mutator:
                owner.pod_mutator(pod, values)
            original.calls.append((args, seconds, output, maximum))
            output.write_bytes(abrupt.encode(dict(items=[pod])))
            return 0
        if args[:4] == ['az', 'storage', 'blob', 'download']:
            name = args[args.index('--name') + 1]
            if name.endswith('start-line.json') and owner.metadata_mutator:
                owner.metadata_mutator(owner, name)
            for repeat, files in owner.publications.items():
                if '/r' + str(repeat) + '/b10-abrupt/first-r' in name:
                    if owner.first_mutator:
                        owner.first_mutator(repeat, files)
                    instance_files(owner, repeat, files)
        return original(instance, args, seconds, output, maximum)

    def collect(instance, job, values):
        if values['mode'] == 'loadgen' or values['mode'] == 'verify':
            path = instance.artifacts / job
            path.mkdir(exist_ok=True)
            pod = scientific_pod(owner, job, values)
            if owner.pod_mutator:
                owner.pod_mutator(pod, values)
            (path / 'pods.json').write_bytes(abrupt.encode(dict(items=[pod])))
    original.collect_hook = collect
    owner.scientific_command = command


def scientific_pod(owner, job, values):
    mode = values['mode']
    role = 'verifier' if mode in ('verify', 'b10-abrupt-verify') else 'loadgen'
    repeat = values['repeat']
    pod_name = ('first-r' + str(repeat) if mode == 'loadgen-restart' else
                'successor-r' + str(repeat) if mode == 'loadgen-successor' else job + '-pod')
    environment = [dict(name='PARCHWELL_SOURCE_COMMIT', value=support.SHA), dict(name='PARCHWELL_JOB_NAME', value=job)]
    if mode in ('loadgen-restart', 'loadgen-successor'):
        environment += [dict(name='B10Abrupt__SourceCommit', value=support.SHA), dict(name='B10Abrupt__JobName', value=job)]
    return dict(metadata=dict(name=pod_name, uid=pod_name + '-uid', ownerReferences=[dict(controller=True, kind='Job', name=job)]),
        spec=dict(serviceAccountName=role, containers=[dict(name=role, image=values[role + '_image'], env=environment)]),
        status=dict(containerStatuses=[dict(name=role, state=dict(terminated=dict(exitCode=1,
                                                   finishedAt='2030-01-01T00:00:00.1234567Z')))]))


def first_publication(owner, repeat):
    """Reuse the valid trial journal vector and bind every scientific identity/hash."""
    fixture = trial_support.WarmupJournalCases()
    fixture.p = owner.p
    fixture.frames = []
    fixture.identity = dict(run_id='b10d-1-1', scenario_id='B10', configuration_id='C1', phase='C1', repeat=1,
        instance_id='first', predecessor_instance_id=None, job_name='b10d-1-1-c1-r1-loadgen-restart', pod_name='first',
        pod_uid='first-uid', source_commit=support.SHA, image_reference=owner.p.read('phase.json')['images']['loadgen']['reference'])
    fixture.report_bytes = abrupt.encode(dict(topics={'document-events': dict(partitions={'0': dict(leader=1)})},
                                              brokers={'one': dict(nodeid=1, state='UP')}))
    fixture.append('child_started', dict(pid=1, start_time_ticks=1, boot_id='boot'))
    fixture.arm('same_instance', 0)
    fixture.append('cut', dict(arm='same_instance', cut_attempt_id='same_instance-4', release_utc=fixture.utc(120),
                              unconfirmed_attempt_ids=['same_instance-' + str(n) for n in range(1, 5)]))
    for n in range(1, 5):
        fixture.original('same_instance', n, 120, resend=True)
    for n in range(1, 5):
        fixture.terminal('same_instance-' + str(n), 121)
    fixture.append('control_drained', dict(flush_returned=True, out_queue_length=0), 121)
    fixture.report('same_instance', 122, post=True)
    fixture.arm('cross_restart', 123)
    fixture.append('kill_intent', dict(arm='cross_restart', cut_attempt_id='cross_restart-4', release_utc=fixture.utc(243)), 123)

    def convert(value):
        if isinstance(value, dict):
            return {key: repeat if key == 'repeat' else convert(item) for key, item in value.items()}
        if isinstance(value, list):
            return list(map(convert, value))
        if isinstance(value, str):
            return (value.replace('b10d-1-1', owner.run_id).replace('-c1-r1-', '-c1-r' + str(repeat) + '-')
                    .replace('/C1/1/', '/C1/' + str(repeat) + '/').replace('diagnostic_trial', 'scientific')
                    .replace('first', 'first-r' + str(repeat)))
        return value
    fixture.frames = convert(fixture.frames)
    files = fixture.publication(copy.deepcopy(fixture.frames))
    instance_files(owner, repeat, files)
    return files


def instance_files(owner, repeat, first):
    prefix = owner.run_id + '/C1/r' + str(repeat) + '/b10-abrupt/'
    for name, raw in first.items():
        owner.fake.blobs[prefix + 'first-r' + str(repeat) + '/' + name] = raw
    identity = json.loads(first['registration.json'])['identity']
    successor_identity = dict(identity, job_name=owner.name('loadgen-successor', repeat),
        pod_name='successor-r' + str(repeat), pod_uid='successor-r' + str(repeat) + '-uid',
        instance_id='successor-r' + str(repeat), predecessor_instance_id=identity['pod_name'])
    registration = abrupt.encode(dict(json.loads(first['registration.json']), identity=successor_identity))
    files = {name: b'{}' for name in abrupt.SUCCESSOR_FILES if name not in ('registration.json', 'completion.json')}
    files['registration.json'] = registration
    data = dict(first_termination=abrupt.descriptor('termination.json', first['termination.json']),
        journal=abrupt.descriptor('journal.jsonl', first['journal.jsonl']),
        kill_unconfirmed_attempt_ids=['cross_restart-' + str(n) for n in range(5)],
        registration=abrupt.descriptor('registration.json', registration),
        artifacts=[abrupt.descriptor(name, raw) for name, raw in files.items() if name != 'registration.json'],
        ready_utc=trial_support.WarmupJournalCases.utc(242), release_utc=trial_support.WarmupJournalCases.utc(243),
        invocations=[dict(attempt_id='successor-' + str(n), predecessor_attempt_id='cross_restart-' + str(n),
            key='B10/' + owner.run_id + '/C1/' + str(repeat) + '/cross_restart', sequence=n, payload_sha256='a' * 64,
            invocation_utc=trial_support.WarmupJournalCases.utc(243), monotonic_ticks=1) for n in range(5)],
        cleanup_completed=True, cross_host_timing='not_verified')
    if owner.successor_mutator:
        owner.successor_mutator(repeat, data)
    files['completion.json'] = abrupt.encode(dict(schema_version=3, dialect='b10_abrupt', purpose='scientific',
        synthetic=True, scientific_admissible=False, kind='successor_receipt', identity=successor_identity, data=data))
    for name, raw in files.items():
        owner.fake.blobs[prefix + successor_identity['pod_name'] + '/' + name] = raw


class ScientificCases(unittest.TestCase):
    setUp = support.PhaseTests.setUp
    main = support.PhaseTests.main
    prepare = support.PhaseTests.prepare
    records = support.PhaseTests.records
    name = support.PhaseTests.name
    wait = support.PhaseTests.wait

    def start(self, configuration='C1'):
        prepare_scientific(self, configuration)

    def run_scientific(self):
        with patch.object(phase.Phase, 'run', autospec=True, side_effect=self.scientific_command):
            return self.main('run-repeats')

    def diagnostics(self, repeat=2):
        return [item for item in self.p.state().get('b10_abrupt_diagnostics', [])
                if item.get('repeat') == repeat]

    def test_c1_jobs_configmaps_lifecycle_and_science_markers(self):
        self.start()
        self.assertEqual(0, self.run_scientific(), self.p.state())
        self.assertEqual([(n, mode) for n in phase.SLOTS for mode in abrupt.Scientific.modes],
                         [(r['repeat'], r['mode']) for r in self.records()])
        self.assertEqual([self.run_id + '-b10-abrupt-' + role + '-r' + str(n)
                          for n in phase.SLOTS for role in ('first', 'successor')], self.configmaps)
        lifecycle = self.p.state()['b10_lifecycle']
        self.assertEqual((15, 5, []), (len(lifecycle['producers']), len(lifecycle['verifiers']), lifecycle['gaps']))
        self.assertEqual(20, len(self.pod_reads))
        self.assertEqual(20, len(set(self.pod_reads)))
        for entry in lifecycle['producers'] + lifecycle['verifiers']:
            self.assertEqual(set('job repeat mode pod_name pod_uid finished_utc exit_code'.split()), set(entry))
            self.assertEqual('2030-01-01T00:00:00.1234567+00:00', entry['finished_utc'])
            self.assertEqual(1, entry['exit_code'])
        document = self.p.read('phase.json')
        self.assertNotIn('b10_abrupt_diagnostic', document)
        self.assertNotIn('purpose', document)
        self.assertNotIn('run_kind', document)
        self.p.reject_b10_abrupt_science()
        for n in phase.SLOTS:
            for mode in ('loadgen-restart', 'loadgen-successor'):
                runtime = json.loads((self.private / ('values-' + self.name(mode, n) + '.json')).read_text())['runtime']
                self.assertIs(True, runtime['b10_abrupt_science'])
                self.assertNotIn('b10_abrupt_diagnostic', runtime)

    def test_c0_fifteen_jobs_and_ten_lifecycle_entries(self):
        self.start('C0')
        self.assertEqual(0, self.run_scientific())
        self.assertEqual([(n, mode) for n in phase.SLOTS for mode in phase.MODES],
                         [(r['repeat'], r['mode']) for r in self.records()])
        lifecycle = self.p.state()['b10_lifecycle']
        self.assertEqual((5, 5, []), (len(lifecycle['producers']), len(lifecycle['verifiers']), lifecycle['gaps']))
        self.assertEqual([], self.configmaps)

    def test_c1_orchestration_values_render_actual_chart_for_every_repeat(self):
        self.start()
        real_run = phase.Phase.run

        def command(instance, args, seconds, output=None, maximum=4 * phase.MIB):
            if args[:2] == ['helm', 'template']:
                return real_run(instance, args, seconds, output, maximum)
            return self.scientific_command(instance, args, seconds, output, maximum)

        with patch.object(phase.Phase, 'run', autospec=True, side_effect=command):
            self.assertEqual(0, self.main('run-repeats'), self.p.state())

        for repeat in phase.SLOTS:
            with self.subTest(repeat=repeat):
                name = self.name('b10-abrupt-verify', repeat)
                runtime = json.loads((self.private / ('values-' + name + '.json')).read_text())['runtime']
                self.assertEqual(repeat, runtime['repeat'])
                self.assertIsNone(runtime['b10_abrupt_diagnostic'])
                rendered = (self.private / ('rendered-' + name + '.yaml')).read_text()
                job = next(document for document in yaml.safe_load_all(rendered)
                           if isinstance(document, dict) and document.get('kind') == 'Job')
                self.assertEqual(name, job['metadata']['name'])
                repeat_entry = next(entry for entry in job['spec']['template']['spec']['containers'][0]['env']
                                    if entry['name'] == 'Verifier__Repeat')
                self.assertEqual(str(repeat), repeat_entry['value'])

    def test_rejected_publication_ends_only_repeat(self):
        self.start()
        def reject(n, files):
            if n == 2:
                document = json.loads(files['termination.json'])
                document['data']['successor_permitted'] = False
                files['termination.json'] = abrupt.encode(document)
        self.first_mutator = reject
        self.assertEqual(1, self.run_scientific())
        self.assert_repeat_failure('b10_abrupt_publication_failed', 'first_publication')
        self.assertEqual(['start-line', 'loadgen', 'loadgen-restart'], self.applied_modes(2))
        self.assertEqual(list(abrupt.Scientific.modes), self.applied_modes(3))
        self.assertFalse(self.p.state()['safety_stop'])

    def assert_repeat_failure(self, reason, boundary):
        self.assertIn((reason, boundary), [(d['reason'], d['boundary']) for d in self.diagnostics()])

    def applied_modes(self, repeat):
        return [r['mode'] for r in self.records() if r['repeat'] == repeat and r['apply'] is not None]

    def assert_later_stopped(self):
        self.assertTrue(self.p.state()['safety_stop'])
        for record in self.records():
            if record['repeat'] >= 3:
                self.assertIsNone(record['apply'])
                self.assertEqual('safety_stop', record['reason'])
        self.assertEqual(15, sum(r['repeat'] >= 3 for r in self.records()))

    def test_render_and_unknown_outcomes_follow_trial_mapping(self):
        cases = [('loadgen', 'render', 1, 'b10_abrupt_configuration_invalid', 'timeout_job', False),
                 ('loadgen-restart', 'render', 1, 'b10_abrupt_first_termination_unknown', 'admission', True),
                 ('loadgen-restart', 'wait', 2, 'b10_abrupt_publication_failed', 'first_termination', True),
                 ('loadgen-successor', 'wait', 2, 'b10_abrupt_publication_ambiguous', 'successor_job', True)]
        for mode, step, code, reason, boundary, stop in cases:
            with self.subTest(mode=mode, step=step):
                self.fake = support.ScriptedCommands()
                self.start()
                self.failures[2, mode, step] = code
                self.assertEqual(1, self.run_scientific())
                self.assert_repeat_failure(reason, boundary)
                self.assertEqual(stop, self.p.state()['safety_stop'])
                if stop:
                    self.assert_later_stopped()
                else:
                    self.assertEqual(list(abrupt.Scientific.modes), self.applied_modes(3))
                self.assertEqual(mode == 'loadgen-successor', 'b10-abrupt-verify' in self.applied_modes(2))

    def test_each_failed_producer_pod_read_stops_later_repeats(self):
        for mode in ('loadgen', 'loadgen-restart', 'loadgen-successor'):
            with self.subTest(mode=mode):
                self.fake = support.ScriptedCommands()
                self.start()
                def mutate(pod, values):
                    if values['repeat'] == 2 and values['mode'] == mode:
                        pod['status']['containerStatuses'][0]['state'].clear()
                self.pod_mutator = mutate
                self.assertEqual(1, self.run_scientific())
                reason = 'b10_abrupt_timeout_job_unknown' if mode == 'loadgen' else 'b10_abrupt_first_termination_unknown'
                self.assertIn(reason, self.p.state()['b10_abrupt_gaps'])
                self.assert_later_stopped()
                self.assertEqual(mode == 'loadgen-successor', 'b10-abrupt-verify' in self.applied_modes(2))
                self.assertEqual([dict(object=self.name(mode, 2) + '/pods.json', reason='producer_termination_unavailable')],
                                 self.p.state()['b10_lifecycle']['gaps'])

    def test_successor_receipt_failure_keeps_verify_and_next_repeat(self):
        self.start()
        self.successor_mutator = lambda n, data: data.update(cleanup_completed=False) if n == 2 else None
        for n, files in self.publications.items():
            instance_files(self, n, files)
        self.assertEqual(1, self.run_scientific())
        self.assert_repeat_failure('b10_abrupt_publication_failed', 'successor_receipt')
        self.assertIn('b10-abrupt-verify', self.applied_modes(2))
        self.assertEqual(list(abrupt.Scientific.modes), self.applied_modes(3))

    def test_first_identity_mismatch_records_trial_admission_failure(self):
        self.start()
        def mismatch(n, files):
            if n == 2:
                document = json.loads(files['termination.json'])
                document['identity']['pod_uid'] = 'other'
                files['termination.json'] = abrupt.encode(document)
        self.first_mutator = mismatch
        self.assertEqual(1, self.run_scientific())
        self.assertEqual([('b10_abrupt_first_termination_unknown', 'admission')],
                         [(d['reason'], d['boundary']) for d in self.diagnostics()])
        self.assert_later_stopped()
        self.assertNotIn('b10-abrupt-verify', self.applied_modes(2))

    def test_metadata_legacy_names_or_missing_object_ends_only_repeat(self):
        for missing in (False, True):
            with self.subTest(missing=missing):
                self.fake = support.ScriptedCommands()
                self.start()
                name = self.run_id + '/C1/r2/start-line.json'
                if missing:
                    self.fake.blobs.pop(name)
                else:
                    document = json.loads(self.fake.blobs[name])
                    document['expected_job_names'] = [self.name('loadgen', 2), self.name('loadgen2', 2)]
                    raw = self.fake.blobs[name] = abrupt.encode(document)
                    completion_name = self.run_id + '/C1/r2/start-line-completion.json'
                    completion = json.loads(self.fake.blobs[completion_name])
                    completion['artifacts'][0] = dict(container='verifier', **abrupt.descriptor(name, raw))
                    self.fake.blobs[completion_name] = abrupt.encode(completion)
                self.assertEqual(1, self.run_scientific())
                self.assert_repeat_failure('b10_abrupt_publication_ambiguous' if missing else 'b10_abrupt_publication_failed', 'metadata')
                self.assertEqual(['start-line'], self.applied_modes(2))
                self.assertEqual(list(abrupt.Scientific.modes), self.applied_modes(3))

    def test_judge_and_candidate_named_gap_remove_stale_candidates(self):
        # The approved final path replaces the temporary named judgement gap.
        for configuration in ('C0', 'C1'):
            self.fake = support.ScriptedCommands()
            self.start(configuration)
            self.assertEqual(0, self.run_scientific())
            before = len(self.records())
            with patch.object(phase.time, 'time', return_value=1_893_456_120), \
                    patch.object(phase.Phase, 'run', autospec=True, side_effect=self.scientific_command):
                self.assertEqual(0, self.main('judge'))
                self.assertEqual(['b10-sweep-line', 'b10-final-sweep', 'judge'],
                                 [record['mode'] for record in self.records()[before:]])
                self.assertNotIn('b10_candidate_gap', self.p.state())
                candidate = support.PhaseTests.candidate(self, None)
                candidate.update(scenario_id='B10', configuration_id=configuration)
                self.fake.download = abrupt.encode(candidate)
                for name in ('candidate.json', 'candidate.json.partial'):
                    (self.candidate_dir / name).write_text('stale')
                self.assertEqual(0, self.main('candidate'))
                self.assertEqual(self.fake.download, (self.candidate_dir / 'candidate.json').read_bytes())
                self.assertFalse((self.candidate_dir / 'candidate.json.partial').exists())
                self.assertEqual('complete_valid', self.p.state()['candidate']['status'])
                # A failed retrieval still removes both stale publication files.
                self.fake.download = b'{}'
                for name in ('candidate.json', 'candidate.json.partial'):
                    (self.candidate_dir / name).write_text('stale')
                with patch.object(phase.Phase, 'sleep'):
                    self.assertEqual(1, self.main('candidate'))
                self.assertFalse((self.candidate_dir / 'candidate.json').exists())
                self.assertFalse((self.candidate_dir / 'candidate.json.partial').exists())
                self.assertNotIn('b10_candidate_gap', self.p.state())
            self.assertTrue(self.output_file.read_text().endswith('full_allowed=false\n'))

    def test_scientific_prepare_ids_fit_pattern_and_reject_overlength(self):
        for configuration in ('C0', 'C1'):
            for identifier in ('1', '40121134428', '9' * 19):
                for attempt in ('1', '12'):
                    with self.subTest(configuration=configuration, identifier=identifier, attempt=attempt), \
                            patch.dict(os.environ, PARCHWELL_SCENARIO='B10', GITHUB_RUN_ID=identifier, GITHUB_RUN_ATTEMPT=attempt):
                        self.assertEqual(0, self.main('prepare', configuration))
                        environment = dict(line.split('=', 1) for line in self.env_file.read_text().splitlines())
                        document = json.loads((support.Path(environment['PARCHWELL_PRIVATE_DIRECTORY']) / 'phase.json').read_text())
                        self.assertRegex(document['run_id'], '^b10-[0-9]+-[0-9]+$')
        with patch.dict(os.environ, PARCHWELL_SCENARIO='B10', GITHUB_RUN_ID='9' * 24, GITHUB_RUN_ATTEMPT='12'):
            self.assertNotEqual(0, self.main('prepare', 'C1'))

    def test_lifecycle_budget_below_at_above_boundary_and_empty_input(self):
        self.start()
        self.p.save_state(dict(self.p.state(), b10_lifecycle=dict(producers=[], verifiers=[], gaps=[])))
        document = self.p.read('phase.json')
        mode, repeat = 'loadgen-restart', 2
        job = self.name(mode, repeat)
        record = dict(job=job, mode=mode, repeat=repeat, apply=0)
        values = self.p.values(mode, repeat)['runtime']
        pod = scientific_pod(self, job, values)
        for initial, elapsed, accepted, reads in ((119, .999, True, 1), (119, 1, True, 1),
                                                  (119, 1.001, False, 1), (120, 0, False, 0)):
            with self.subTest(initial=initial, elapsed=elapsed):
                clock = [100.0]
                budget = [initial]
                state = self.p.state()
                state['b10_lifecycle'] = dict(producers=[], verifiers=[], gaps=[])
                self.p.save_state(state)
                def command(args, seconds, output=None, **_):
                    output.write_bytes(abrupt.encode(dict(items=[pod])))
                    clock[0] += elapsed
                    return 0
                with patch.object(phase.time, 'monotonic', side_effect=lambda: clock[0]), \
                        patch.object(self.p, 'run', side_effect=command) as query:
                    if accepted:
                        self.p.b10_scientific_observe(record, 1000, budget)
                    else:
                        with self.assertRaisesRegex(ValueError, '^lifecycle deadline$'):
                            self.p.b10_scientific_observe(record, 1000, budget)
                self.assertEqual(reads, query.call_count)
                self.assertEqual(initial + elapsed, budget[0])
                self.assertEqual(int(accepted), len(self.p.state()['b10_lifecycle']['producers']))
                self.assertEqual(int(not accepted), len(self.p.state()['b10_lifecycle']['gaps']))
        state = self.p.state()
        state['b10_lifecycle'] = dict(producers=[], verifiers=[], gaps=[])
        self.p.save_state(state)
        with patch.object(self.p, 'run') as query:
            self.assertIsNone(self.p.b10_scientific_observe(dict(record, apply=None), 1000, [0]))
        query.assert_not_called()
        self.assertEqual([], self.p.state()['b10_lifecycle']['gaps'])

    def test_verifier_lifecycle_gap_fails_exit_without_stopping_next_repeat(self):
        self.start()
        def mutate(pod, values):
            if values['repeat'] == 2 and values['mode'] == 'b10-abrupt-verify':
                pod['status']['containerStatuses'][0]['state'].clear()
        self.pod_mutator = mutate
        self.assertEqual(1, self.run_scientific())
        self.assertFalse(self.p.state()['safety_stop'])
        self.assertEqual([dict(object=self.name('b10-abrupt-verify', 2) + '/pods.json', reason='verifier_termination_unavailable')],
                         self.p.state()['b10_lifecycle']['gaps'])
        self.assertEqual(list(abrupt.Scientific.modes), self.applied_modes(3))

    def test_deadline_records_all_later_slots_and_keeps_one_phase_clock(self):
        self.start()
        clock = [100.0]
        original = self.scientific_command
        def exhaust(instance, args, *positional, **keywords):
            result = original(instance, args, *positional, **keywords)
            if 'collect-job' in args and args[-1] == self.name('b10-abrupt-verify', 1):
                clock[0] = 18100.0
            return result
        self.scientific_command = exhaust
        with patch.object(phase.time, 'monotonic', side_effect=lambda: clock[0]):
            self.assertEqual(1, self.run_scientific())
        self.assertEqual(25, len(self.records()))
        for record in self.records()[5:]:
            self.assertEqual(('not_attempted', 'orchestration_deadline'), (record['outcome'], record['reason']))
            self.assertIsNone(record['apply'])
        clock = self.p.read('phase.json')['b10_clock']
        self.assertEqual(18000, clock['deadline'] - clock['started'])

    def test_verify_failure_and_missing_output_follow_trial_boundaries(self):
        for missing in (False, True):
            with self.subTest(missing=missing):
                self.fake = support.ScriptedCommands()
                self.start()
                if missing:
                    self.fake.blobs.pop(self.run_id + '/C1/r2/b10-abrupt/nonfinal-result.json')
                else:
                    self.failures[2, 'b10-abrupt-verify', 'wait'] = 1
                self.assertEqual(1, self.run_scientific())
                self.assert_repeat_failure('b10_abrupt_verify_failed', 'verify_collection' if missing else 'verify_job')
                self.assertEqual(list(abrupt.Scientific.modes), self.applied_modes(3))

    def test_scientific_readers_reject_other_purpose_and_repeat(self):
        self.start()
        for bad_purpose, bad_repeat in (('diagnostic_trial', 3), ('scientific', 2)):
            scientific = abrupt.Scientific(self.p, phase.Failure, 3, 1000, [0])
            files = self.publications[3]
            raw = json.loads(files['registration.json'])
            raw['purpose'] = bad_purpose
            raw['identity']['repeat'] = bad_repeat
            observation = {key: raw['identity'][key] for key in ('job_name', 'pod_name', 'pod_uid')}
            with self.assertRaisesRegex(ValueError, '^diagnostic envelope$' if bad_purpose != 'scientific' else '^diagnostic identity$'):
                scientific.envelope(abrupt.encode(raw), 'registration', observation)

    def test_trial_producer_predicates_remain_mode_specific(self):
        self.start()
        def mutate(pod, values):
            if values['mode'] not in ('loadgen', 'loadgen-restart', 'loadgen-successor'):
                return
            pod['spec'].pop('serviceAccountName')
            pod['spec']['containers'][0]['env'] = [entry for entry in pod['spec']['containers'][0]['env']
                                                   if entry['name'].startswith('B10Abrupt__')]
        self.pod_mutator = mutate
        self.assertEqual(0, self.run_scientific())

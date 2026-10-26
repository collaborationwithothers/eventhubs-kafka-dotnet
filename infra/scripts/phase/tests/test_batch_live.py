"""Literal synthetic transport/deadline regressions for the real single-action bridge."""
import io
import json
import os
from pathlib import Path
import unittest

import yaml
from unittest.mock import Mock, patch

import test_phase as fixture

phase = fixture.phase


class BatchLiveTests(unittest.TestCase):
    setUp = fixture.PhaseTests.setUp
    main = fixture.PhaseTests.main
    mocked = fixture.PhaseTests.mocked
    records = fixture.PhaseTests.records
    prepare_batch = fixture.PhaseTests.prepare_batch
    batch_name = fixture.PhaseTests.batch_name

    def prepare_live(self):
        self.prepare_batch()
        value = phase.Phase().read('phase.json')
        value['run_kind'] = 'batch'
        phase.write_json(self.private / 'phase.json', value)
        os.environ['PARCHWELL_RUN_KIND'] = 'batch'

    def seed_artifacts(self, mutation=None):
        def mutate(job, pods, collection, logs):
            pod = pods['items'][0]
            pod['metadata']['ownerReferences'] = [{'kind': 'Job', 'name': job, 'controller': True}]
            receipt = {'schema_version': 1, 'mode': 'single-action-seed', 'run_id': self.run_id,
                'source_commit': fixture.SHA, 'job_name': job, 'pod_name': job + '-pod',
                'pod_uid': pod['metadata']['uid'], 'service_account': 'enrichment-consumer',
                'topic': 'document-state', 'partition': 0, 'key_base64': 'synthetic',
                'value_sha256': '68fe9c9f499dd1a6bf07c6f737c2b6a8118cca37877336ee0f2674209554d3c6',
                'delivered': False, 'error': {'code': 29, 'is_fatal': False, 'origin': 'broker', 'reason': 'synthetic'},
                'handler_exit_code': 0, 'cleanup_completed': True}
            logs[job + '-pod.containers.enrichment-consumer.log'] = phase.SEED_RECEIPT_MARKER + json.dumps(receipt) + '\n'
            if mutation:
                mutation(pods, logs)
        self.fake.artifact_mutator = mutate

    def literal_candidate(self, item='B01', configuration='C0'):
        return {'schema_version': 1, 'mode': 'single-action', 'synthetic': True, 'run_id': self.run_id,
            'scenario_id': item, 'configuration_id': configuration, 'source_commit': fixture.SHA,
            'managed_version': '2.16.0', 'native_version': '2.16.0', 'image_digests': fixture.BATCH_DIGESTS,
            'status': 'incomplete', 'repeats': [{'repeat': number, 'status': 'incomplete',
                'reasons': ['p2_completion'], 'outcome_class': None, 'broker_count': None, 'statistics_reference': None}
                for number in range(1, 6)], 'deterministic': None, 'outcome_class': None,
            'adr012_counts': 'not_applicable', 'diagnostics': [], 'security_finding': False, 'item_assessment': None}

    def literal_receipt(self, item='B01', configuration='C0'):
        job = self.batch_name('single-action-judge', item, configuration)
        return {'schema_version': 1, 'mode': 'single-action-judge', 'run_id': self.run_id, 'scenario_id': item,
            'configuration_id': configuration, 'source_commit': fixture.SHA, 'job_name': job,
            'pod_name': job + '-pod', 'pod_uid': '00000000-0000-4000-8000-000000000213',
            'candidate_name': f'{self.run_id}/single-action/{item}/{configuration}/candidate.json',
            'handler_exit_code': 0, 'cleanup_completed': True}

    def candidate_commands(self, mutate=None):
        original = self.fake
        def command(instance, args, seconds, output=None, maximum=4 * phase.MIB):
            args = list(map(str, args))
            if args[:4] in (['az', 'storage', 'blob', 'show'], ['az', 'storage', 'blob', 'download']):
                name = args[args.index('--name') + 1]
                _, marker, item, config, filename = name.split('/')
                self.assertEqual('single-action', marker)
                value = self.literal_candidate(item, config) if filename == 'candidate.json' else self.literal_receipt(item, config)
                if mutate:
                    mutate(filename, value)
                original.download = json.dumps(value).encode()
            return original(instance, args, seconds, output, maximum)
        return command

    def test_batch_selection_preserves_live_run_kind(self):
        self.prepare_live()
        self.assertEqual('batch', phase.Phase().read('phase.json')['run_kind'])
        self.assertEqual(0, self.main('validate-batch'))
        action = phase.Phase().values('single-action', None, 'B01', 'C0', 'loadgen')['runtime']
        self.assertIs(False, action['image_smoke'])
        self.assertEqual(fixture.BATCH_DIGESTS['enrichment'], action['enrichment_image'].split('@')[1])
        judge = phase.Phase().values('single-action-judge', None, 'F04', 'C4', 'verifier')['runtime']
        self.assertEqual(self.run_id + '-f04-seed-receipt', judge['seed_receipt_configmap'])

    def test_batch_publishes_and_reads_back_exactly_three_real_images(self):
        self.prepare_live()
        with self.mocked():
            self.assertEqual(0, self.main('publish-images'))
        publishes = [call[0] for call in self.fake.calls if call[0][:2] == ['dotnet', 'publish']]
        self.assertEqual(['src/Parchwell.LoadGen/Parchwell.LoadGen.csproj',
            'src/Parchwell.Verifier/Parchwell.Verifier.csproj',
            'src/Parchwell.Enrichment/Parchwell.Enrichment.csproj'], [call[2] for call in publishes])
        self.assertTrue(all('-p:ContainerImageTag=' + fixture.SHA in call for call in publishes))
        self.assertEqual(set(fixture.BATCH_DIGESTS), set(phase.Phase().read('phase.json')['images']))
        self.assertEqual(3, sum(call[0][:4] == ['az', 'acr', 'repository', 'show'] for call in self.fake.calls))

    def test_linux_boot_identity_reads_literal_proc_path_with_fixed_bound(self):
        identity = '01234567-89ab-4cde-8fab-0123456789ab'
        path = Mock()
        path.open.return_value = io.BytesIO((identity + '\n').encode())
        with patch.object(phase.sys, 'platform', 'linux'), patch.object(phase, 'Path', return_value=path) as filename, patch.object(phase.ctypes, 'CDLL') as library:
            self.assertEqual(identity, phase.Phase.boot_identity())
            filename.assert_called_once_with('/proc/sys/kernel/random/boot_id')
            path.open.assert_called_once_with('rb')
            library.assert_not_called()
        for content in (b'', b's' * 65, b'not-a-boot-uuid', b'\xff'):
            with self.subTest(content=content), patch.object(phase.sys, 'platform', 'linux'), patch.object(phase.Path, 'open', return_value=io.BytesIO(content)):
                self.assertIsNone(phase.Phase.boot_identity())
        with patch.object(phase.sys, 'platform', 'linux'), patch.object(phase.Path, 'open', side_effect=OSError):
            self.assertIsNone(phase.Phase.boot_identity())

    def test_darwin_boot_identity_uses_fixed_size_read_only_sysctlbyname(self):
        identity = '01234567-89AB-4CDE-8FAB-0123456789AB'
        for result, reported_length, content, expected in ((0, 37, identity.encode() + b'\0', identity.lower()),
                (1, 37, identity.encode() + b'\0', None), (0, 65, identity.encode() + b'\0', None),
                (0, 0, b'', None), (0, 4, b'bad\0', None)):
            with self.subTest(result=result, length=reported_length):
                def read_value(name, buffer, length, new_value, new_length):
                    self.assertEqual(b'kern.bootsessionuuid', name)
                    self.assertEqual(64, len(buffer))
                    self.assertEqual(64, phase.ctypes.cast(length, phase.ctypes.POINTER(phase.ctypes.c_size_t)).contents.value)
                    self.assertIsNone(new_value)
                    self.assertEqual(0, new_length)
                    buffer.raw = content.ljust(64, b'\0')
                    phase.ctypes.cast(length, phase.ctypes.POINTER(phase.ctypes.c_size_t)).contents.value = reported_length
                    return result
                library = Mock()
                library.sysctlbyname.side_effect = read_value
                with patch.object(phase.sys, 'platform', 'darwin'), patch.object(phase.ctypes, 'CDLL', return_value=library) as load, patch.object(phase.subprocess, 'call') as child:
                    self.assertEqual(expected, phase.Phase.boot_identity())
                    load.assert_called_once_with(None, use_errno=True)
                    child.assert_not_called()
        with patch.object(phase.sys, 'platform', 'darwin'), patch.object(phase.ctypes, 'CDLL', side_effect=OSError):
            self.assertIsNone(phase.Phase.boot_identity())
        with patch.object(phase.sys, 'platform', 'unknown'):
            self.assertIsNone(phase.Phase.boot_identity())

    def test_unavailable_boot_identity_records_seed_and_all_configuration_gaps(self):
        self.prepare_live()
        with self.mocked(), patch.object(phase.Phase, 'boot_identity', return_value=None):
            self.assertEqual(0, self.main('single-action-seed'))
            self.assertEqual(1, self.main('run-single-action-batch'))
        self.assertEqual(33, len(self.records()))
        self.assertTrue(all(record['outcome'] == 'not_attempted' and record['reason'] == 'start_cap' for record in self.records()))
        self.assertTrue(phase.Phase().state()['seed_gap'])
        self.assertEqual(16, len(list(self.candidate_dir.glob('*-gap.json'))))
        self.assertEqual([], self.fake.calls)

    def test_shared_clock_survives_cli_instances_and_includes_seed_time(self):
        self.prepare_live()
        self.seed_artifacts()
        with self.mocked(), patch.object(phase.time, 'monotonic', return_value=100):
            self.assertEqual(0, self.main('single-action-seed'))
        data = phase.Phase().read('phase.json')
        self.assertEqual(100, data['batch_started_monotonic'])
        self.assertEqual('CLOCK_MONOTONIC', data['batch_clock_source'])
        with patch.object(phase.time, 'monotonic', return_value=14500):
            self.assertFalse(phase.Phase().admission_allowed())
        self.assertEqual(100, phase.Phase().read('phase.json')['batch_started_monotonic'])

    def test_missing_invalid_or_wrong_boot_clock_preserves_all_sixteen_gaps(self):
        cases = [('missing', None), ('boolean', True), ('future', 10**1000), ('negative', -1), ('boot', 0), ('run', 0), ('clock', 0)]
        for kind, started in cases:
            with self.subTest(kind=kind):
                self.prepare_live()
                data = phase.Phase().read('phase.json')
                data['batch_started_monotonic'] = started
                if kind == 'boot': data['batch_clock_boot_id'] = 'different-boot'
                if kind == 'run': data['batch_clock_run_id'] = 'different-run'
                if kind == 'clock': data['batch_clock_source'] = 'wall-clock'
                phase.write_json(self.private / 'phase.json', data)
                self.fake = fixture.ScriptedCommands()
                with self.mocked():
                    self.assertEqual(1, self.main('run-single-action-batch'))
                self.assertEqual(32, len(self.records()))
                self.assertTrue(all(record['outcome'] == 'not_attempted' and record['reason'] == 'start_cap' for record in self.records()))
                self.assertEqual(16, len(list(self.candidate_dir.glob('*-gap.json'))))
                self.assertEqual([], self.fake.calls)

    def test_command_timeout_reserves_five_seconds_and_never_starts_at_reserve(self):
        self.prepare_live()
        instance = phase.Phase()
        instance.command_deadline = 100
        for now, expected in [(89, 6), (94, 1), (95, None), (96, None), (100, None)]:
            with self.subTest(now=now), patch.object(phase.time, 'monotonic', return_value=now), patch.object(phase.subprocess, 'call', return_value=0) as child:
                self.assertEqual(124 if expected is None else 0, instance.run(['synthetic'], 60))
                if expected is None:
                    child.assert_not_called()
                else:
                    self.assertEqual(str(expected), child.call_args.args[0][2])
        with patch.object(phase.time, 'monotonic', return_value=0), patch.object(phase.subprocess, 'call', return_value=0) as child:
            instance.run(['synthetic'], 20)
            self.assertEqual('20', child.call_args.args[0][2])

    def test_expired_pair_stops_admissions_when_pods_cannot_be_confirmed(self):
        self.prepare_live()
        now = [100.0]
        original = self.fake
        def command(instance, args, seconds, output=None, maximum=4 * phase.MIB):
            result = original(instance, args, seconds, output, maximum)
            if args[:2] == ['kubectl', 'apply']:
                now[0] += 3360
                return 124
            if args[:3] == ['kubectl', 'get', 'pods']:
                return 124
            return result
        with patch.object(phase.Phase, 'run', autospec=True, side_effect=command), patch.object(phase.time, 'monotonic', side_effect=lambda: now[0]):
            # Reset clock into this literal monotonic epoch.
            phase.Phase().start_batch_clock()
            self.assertEqual(1, self.main('run-single-action-batch'))
        self.assertTrue(phase.Phase().state()['safety_stop'])
        self.assertEqual(32, len(self.records()))
        self.assertEqual('orchestration_deadline', self.records()[0]['reason'])
        self.assertTrue(all(record['reason'] == 'safety_stop' for record in self.records()[1:]))
        self.assertEqual(1, sum(call[0][:2] == ['kubectl', 'apply'] for call in self.fake.calls))

    def test_seed_receipt_transfer_preserves_failed_delivery_raw_without_judging_it(self):
        self.prepare_live()
        self.seed_artifacts()
        with self.mocked(): self.assertEqual(0, self.main('single-action-seed'))
        self.assertFalse(phase.Phase().state()['seed_gap'])
        configmap = phase.Phase().read('seed-receipt-configmap.json')
        self.assertTrue(configmap['immutable'])
        self.assertEqual(self.run_id + '-f04-seed-receipt', configmap['metadata']['name'])
        receipt = json.loads(configmap['data']['receipt.json'])
        self.assertFalse(receipt['delivered'])
        envelope = json.loads(configmap['data']['pod.json'])
        self.assertEqual({'schema_version', 'job_name', 'pod_name', 'pod_uid', 'service_account', 'owner_kind', 'owner_name'}, set(envelope))
        self.assertEqual('Job', envelope['owner_kind'])
        self.assertEqual('enrichment-consumer', envelope['service_account'])
        creates = [call for call in self.fake.calls if call[0][:2] == ['kubectl', 'create']]
        self.assertEqual(1, len(creates))
        self.assertEqual((60, '--request-timeout=30s'), (creates[0][1], creates[0][0][-1]))

    def test_seed_transfer_rejects_duplicate_missing_oversized_smoke_and_bad_pod_evidence(self):
        for kind in ('missing', 'duplicate', 'oversized', 'smoke', 'pods', 'owner', 'unknown-owner', 'bad-json'):
            with self.subTest(kind=kind):
                self.prepare_live()
                self.fake = fixture.ScriptedCommands()
                def mutate(pods, logs):
                    key = next(name for name in logs if '.containers.' in name)
                    if kind == 'missing': logs[key] = ''
                    if kind == 'duplicate': logs[key] *= 2
                    if kind == 'oversized': logs[key] = phase.SEED_RECEIPT_MARKER + '{"x":"' + 's' * 65536 + '"}\n'
                    if kind == 'smoke': logs[key] = json.dumps(phase.SEED_SMOKE_MARKER) + '\n'
                    if kind == 'pods': pods['items'] *= 2
                    if kind == 'owner': pods['items'][0]['metadata']['ownerReferences'] = []
                    if kind == 'unknown-owner': pods['items'][0]['metadata']['ownerReferences'][0]['name'] = 'other-job'
                    if kind == 'bad-json': logs[key] = phase.SEED_RECEIPT_MARKER + '{bad-json}\n'
                self.seed_artifacts(mutate)
                with self.mocked(): self.assertEqual(0, self.main('single-action-seed'))
                self.assertTrue(phase.Phase().state()['seed_gap'])
                self.assertTrue((self.artifacts / 'seed-gap.json').exists())
                self.assertFalse((self.private / 'seed-receipt-configmap.json').exists())
                self.assertFalse(any(call[0][:2] == ['kubectl', 'create'] for call in self.fake.calls))

    def test_seed_receipt_line_byte_boundary_and_whole_deadline(self):
        for length, accepted in ((65535, True), (65536, True), (65537, False)):
            with self.subTest(length=length):
                self.prepare_live()
                self.fake = fixture.ScriptedCommands()
                def mutate(_pods, logs):
                    key = next(name for name in logs if '.containers.' in name)
                    head = phase.SEED_RECEIPT_MARKER + '{"raw":"'
                    tail = '"}\n'
                    logs[key] = head + 's' * (length - len(head) - len(tail)) + tail
                    self.assertEqual(length, len(logs[key].encode()))
                self.seed_artifacts(mutate)
                with self.mocked(): self.assertEqual(0, self.main('single-action-seed'))
                self.assertEqual(not accepted, phase.Phase().state()['seed_gap'])
                self.assertEqual(accepted, (self.private / 'seed-receipt-configmap.json').exists())
        self.prepare_live()
        now = [100.0]
        deadlines = []
        original = self.fake
        def command(instance, args, seconds, output=None, maximum=4 * phase.MIB):
            deadlines.append(instance.command_deadline)
            result = original(instance, args, seconds, output, maximum)
            if args[0] == 'helm':
                now[0] += 1050
            return result
        with patch.object(phase.Phase, 'run', autospec=True, side_effect=command), patch.object(phase.time, 'monotonic', side_effect=lambda: now[0]):
            self.assertEqual(0, self.main('single-action-seed'))
        self.assertTrue(deadlines)
        self.assertTrue(all(deadline == 1150 for deadline in deadlines))
        self.assertEqual('orchestration_deadline', self.records()[0]['reason'])
        self.assertTrue(phase.Phase().state()['seed_gap'])

    def test_blob_size_bounds_reject_before_download_and_keep_no_partial(self):
        self.prepare_live()
        for maximum in (16777216, 65536):
            for length in (0, maximum + 1, True, '65536'):
                with self.subTest(maximum=maximum, length=length):
                    self.fake = fixture.ScriptedCommands()
                    self.fake.properties = [(0, json.dumps({'etag': 'synthetic-etag', 'contentLength': length}))]
                    target = self.candidate_dir / 'bounded.json'
                    with self.mocked():
                        self.assertIsNone(phase.Phase().download_blob('synthetic/candidate.json', target, maximum, None))
                    self.assertEqual(1, len(self.fake.calls))
                    self.assertFalse(target.exists())
                    self.assertFalse(Path(str(target) + '.partial').exists())
        for length in (65535, 65536):
            self.fake = fixture.ScriptedCommands()
            self.fake.download = b's' * length
            target = self.candidate_dir / 'bounded.json'
            target.unlink(missing_ok=True)
            with self.mocked():
                self.assertEqual(self.fake.download, phase.Phase().download_blob('synthetic/receipt.json', target, 65536, None))
            self.assertEqual(length, target.stat().st_size)

    def test_live_incomplete_candidates_and_capture_failure_continue_all_configurations(self):
        self.prepare_live()
        capture = self.batch_name('single-action')
        self.fake.responses[('bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job', capture, '1980')] = (1, '')
        with patch.object(phase.Phase, 'run', autospec=True, side_effect=self.candidate_commands()):
            self.assertEqual(0, self.main('run-single-action-batch'))
        self.assertEqual(32, len(self.records()))
        self.assertEqual('failed', self.records()[0]['outcome'])
        self.assertEqual(16, len(phase.Phase().state()['candidates']))
        self.assertTrue(all(candidate['accepted'] for candidate in phase.Phase().state()['candidates']))
        self.assertEqual({f'{item}-{config}{suffix}' for item, config, _ in fixture.BATCH_MATRIX
            for suffix in ('.json', '-judge-completion.json')}, {path.name for path in self.candidate_dir.iterdir()})
        self.assertEqual(32, sum(call[0][:4] == ['az', 'storage', 'blob', 'download'] for call in self.fake.calls))
        for name in self.candidate_dir.glob('*.json'):
            self.assertFalse(name.name.startswith(self.run_id))

    def test_transport_rejects_unknown_keys_bad_source_and_unsuccessful_final_receipts(self):
        cases = ('candidate-extra', 'nested-extra', 'candidate-source', 'receipt-extra', 'exit', 'cleanup', 'pod', 'candidate-name', 'digests')
        for kind in cases:
            with self.subTest(kind=kind):
                self.prepare_live()
                self.fake = fixture.ScriptedCommands()
                def mutate(filename, value):
                    if filename == 'candidate.json':
                        if kind == 'candidate-extra': value['unexpected'] = True
                        if kind == 'nested-extra': value['repeats'][0]['unexpected'] = True
                        if kind == 'candidate-source': value['source_commit'] = 'e' * 40
                        if kind == 'digests': value['image_digests'] = fixture.DIGESTS
                    else:
                        if kind == 'receipt-extra': value['unexpected'] = True
                        if kind == 'exit': value['handler_exit_code'] = 1
                        if kind == 'cleanup': value['cleanup_completed'] = False
                        if kind == 'pod': value['pod_uid'] = 'different-pod'
                        if kind == 'candidate-name': value['candidate_name'] = 'legacy/candidate.json'
                with patch.object(phase.Phase, 'run', autospec=True, side_effect=self.candidate_commands(mutate)):
                    self.assertEqual(1, self.main('run-single-action-batch'))
                self.assertEqual(32, len(self.records()))
                self.assertTrue(all(not candidate['accepted'] for candidate in phase.Phase().state()['candidates']))
                self.assertEqual(16, len(list(self.candidate_dir.glob('*-gap.json'))))
                self.assertEqual(32, len([p for p in self.candidate_dir.glob('*.json') if not p.name.endswith('-gap.json')]))

    def test_real_live_helm_output_uses_actual_images_modes_and_optional_seed_mount(self):
        self.prepare_live()
        cases = [('single-action-seed', 'F04', 'C2', 'enrichment-consumer', 420, 'enrichment'),
                 ('single-action', 'F04', 'C2', 'probe', 1980, 'loadgen'),
                 ('single-action-judge', 'F04', 'C4', 'verifier', 120, 'verifier'),
                 ('single-action-judge', 'B01', 'C0', 'verifier', 120, 'verifier')]
        for mode, item, config, role, deadline, worker in cases:
            with self.subTest(mode=mode, item=item):
                self.assertEqual(deadline, phase.Phase().render(mode, None, item, config, role))
                path = self.private / f'rendered-{self.batch_name(mode, item, config)}.yaml'
                job = next(document for document in yaml.safe_load_all(path.read_text()) if document and document['kind'] == 'Job')
                pod = job['spec']['template']['spec']
                container = pod['containers'][0]
                self.assertEqual(f'parchwellacr.azurecr.io/parchwell/{worker}@{fixture.BATCH_DIGESTS[worker]}', container['image'])
                self.assertEqual(['--single-action-seed'] if worker == 'enrichment' else ['--single-action'] if mode == 'single-action' else None, container.get('args'))
                env = {entry['name']: entry for entry in container['env']}
                if worker == 'enrichment':
                    self.assertFalse(any(name.startswith('Telemetry__') for name in env))
                    self.assertEqual('330000', env['SeedF04__ActionWindowMs']['value'])
                if mode == 'single-action-judge':
                    self.assertEqual('single-action-judge', env['Verifier__Mode']['value'])
                    self.assertEqual(f'{self.run_id}/single-action/{item}/{config}/', env['Verifier__CapturePrefix']['value'])
                mounts = [volume for volume in pod['volumes'] if volume['name'] == 'seed-receipt']
                if mode == 'single-action-judge' and item == 'F04':
                    self.assertEqual(self.run_id + '-f04-seed-receipt', mounts[0]['configMap']['name'])
                    self.assertTrue(mounts[0]['configMap']['optional'])
                else:
                    self.assertEqual([], mounts)

    def test_new_transport_parser_has_literal_depth_bound_and_ignores_escaped_string_brackets(self):
        for depth in (31, 32):
            with self.subTest(depth=depth):
                text = '[' * depth + '0' + ']' * depth
                self.assertIsInstance(phase.single_action_json(text.encode()), list)
        for depth in (33, 11000):
            with self.subTest(depth=depth):
                text = '[' * depth + '0' + ']' * depth
                self.assertLess(len(text), 65536)
                with self.assertRaises(phase.Failure):
                    phase.single_action_json(text)
        strings = ['[' * 11000 + ']' * 11000, '\\' * 40 + '"quoted [ }"', 'escaped \\" [ \\" ]']
        for value in strings:
            text = json.dumps({'literal': value})
            self.assertEqual({'literal': value}, phase.single_action_json(text))
        for text in (b'{"invalid":"\xff"}', b'{"duplicate":0,"duplicate":1}', b'{"malformed":', b']'):
            with self.subTest(text=text), self.assertRaises((phase.Failure, ValueError, UnicodeError)):
                phase.single_action_json(text)
        with patch.object(phase, 'strict_json', side_effect=RecursionError), self.assertRaises(phase.Failure):
            phase.single_action_json('{}')
        # The legacy parser retains its previous unbounded-nesting semantics.
        self.assertIsInstance(phase.strict_json('[' * 33 + '0' + ']' * 33), list)

    def test_deep_candidate_and_receipt_failures_preserve_all_thirtytwo_jobs_and_sixteen_gaps(self):
        cases = [('valid', None, 0), ('escaped-string', None, 0)]
        cases += [(filename, filename, depth) for filename in ('candidate.json', 'judge-completion.json') for depth in (31, 32, 33, 11000)]
        cases += [(filename + '-utf8', filename, -1) for filename in ('candidate.json', 'judge-completion.json')]
        for kind, damaged, depth in cases:
            with self.subTest(kind=kind, depth=depth):
                self.prepare_live()
                self.fake = fixture.ScriptedCommands()
                def download(_instance, name, target, maximum, _deadline):
                    _, _, item, config, filename = name.split('/')
                    value = self.literal_candidate(item, config) if filename == 'candidate.json' else self.literal_receipt(item, config)
                    if kind == 'escaped-string' and filename == 'candidate.json':
                        value['diagnostics'] = ['[' * 11000 + '\\"escaped"' + ']' * 11000]
                    text = json.dumps(value).encode()
                    if filename == damaged:
                        payload = b'"\xff"' if depth == -1 else b'[' * (depth - 1) + b'0' + b']' * (depth - 1)
                        text = text[:-1] + b',"unexpected":' + payload + b'}'
                    self.assertLess(len(text), maximum)
                    target.write_bytes(text)
                    return text
                with self.mocked(), patch.object(phase.Phase, 'download_blob', autospec=True, side_effect=download):
                    self.assertEqual(0 if damaged is None else 1, self.main('run-single-action-batch'))
                self.assertEqual(32, len(self.records()))
                self.assertEqual(32, sum(call[0][:2] == ['kubectl', 'apply'] for call in self.fake.calls))
                candidates = phase.Phase().state()['candidates']
                self.assertEqual(16, len(candidates))
                self.assertTrue(all(candidate['accepted'] is (damaged is None) for candidate in candidates))
                self.assertEqual(0 if damaged is None else 16, len(list(self.candidate_dir.glob('*-gap.json'))))

    def test_deep_seed_receipt_gap_does_not_skip_unrelated_configuration_paths(self):
        for depth, gap in ((31, False), (32, False), (33, True), (11000, True), (-1, True)):
            with self.subTest(depth=depth):
                self.prepare_live()
                self.fake = fixture.ScriptedCommands()
                def mutate(_pods, logs):
                    key = next(name for name in logs if '.containers.' in name)
                    text = '{"unexpected":' + '[' * (depth - 1) + '0' + ']' * (depth - 1) + '}\n'
                    logs[key] = phase.SEED_RECEIPT_MARKER + text if depth != -1 else (phase.SEED_RECEIPT_MARKER + '{bad-json}\n')
                    self.assertLess(len(logs[key].encode()), 65536)
                self.seed_artifacts(mutate)
                with self.mocked():
                    self.assertEqual(0, self.main('single-action-seed'))
                self.assertIs(gap, phase.Phase().state()['seed_gap'])
                self.assertEqual(gap, (self.artifacts / 'seed-gap.json').exists())
                self.fake.artifact_mutator = None
                with patch.object(phase.Phase, 'run', autospec=True, side_effect=self.candidate_commands()):
                    self.assertEqual(0, self.main('run-single-action-batch'))
                self.assertEqual(33, len(self.records()))
                self.assertEqual(16, len(phase.Phase().state()['candidates']))
                self.assertTrue(all(candidate['accepted'] for candidate in phase.Phase().state()['candidates']))
                self.assertEqual(33, sum(call[0][:2] == ['kubectl', 'apply'] for call in self.fake.calls))

    def test_deep_blob_properties_and_judge_pod_json_are_controlled_transport_gaps(self):
        for kind in ('properties', 'pods', 'pods-utf8'):
            with self.subTest(kind=kind):
                self.prepare_live()
                self.fake = fixture.ScriptedCommands()
                original = self.candidate_commands()
                def command(instance, args, seconds, output=None, maximum=4 * phase.MIB):
                    result = original(instance, args, seconds, output, maximum)
                    if kind == 'properties' and args[:4] == ['az', 'storage', 'blob', 'show']:
                        Path(output).write_bytes(b'{"unexpected":' + b'[' * 11000 + b'0' + b']' * 11000 + b'}')
                    if kind.startswith('pods') and len(args) == 4 and args[:3] == ['bash', 'infra/scripts/lifecycle/lifecycle.sh', 'collect-job'] and args[-1].endswith('single-judge'):
                        path = self.artifacts / args[-1] / 'pods.json'
                        content = path.read_bytes()
                        extra = b'[' * 11000 + b'0' + b']' * 11000 if kind == 'pods' else b'"\xff"'
                        path.write_bytes(content[:-1] + b',"unexpected":' + extra + b'}')
                    return result
                with patch.object(phase.Phase, 'run', autospec=True, side_effect=command):
                    self.assertEqual(1, self.main('run-single-action-batch'))
                self.assertEqual(32, len(self.records()))
                self.assertEqual(16, len(phase.Phase().state()['candidates']))
                self.assertEqual(16, len(list(self.candidate_dir.glob('*-gap.json'))))
                self.assertTrue(all(not candidate['accepted'] for candidate in phase.Phase().state()['candidates']))

    def test_deep_seed_pod_json_and_wrong_owner_types_record_seed_gap(self):
        for kind in ('deep', 'utf8', 'receipt-utf8', 'owner-type', 'uid-type'):
            with self.subTest(kind=kind):
                self.prepare_live()
                self.fake = fixture.ScriptedCommands()
                def mutate(pods, _logs):
                    if kind == 'owner-type': pods['items'][0]['metadata']['ownerReferences'] = [1]
                    if kind == 'uid-type': pods['items'][0]['metadata']['uid'] = {'wrong': 'type'}
                self.seed_artifacts(mutate)
                original = self.fake
                def command(instance, args, seconds, output=None, maximum=4 * phase.MIB):
                    result = original(instance, args, seconds, output, maximum)
                    if kind in ('deep', 'utf8') and len(args) == 4 and args[:3] == ['bash', 'infra/scripts/lifecycle/lifecycle.sh', 'collect-job']:
                        path = self.artifacts / args[-1] / 'pods.json'
                        content = path.read_bytes()
                        extra = b'[' * 11000 + b'0' + b']' * 11000 if kind == 'deep' else b'"\xff"'
                        path.write_bytes(content[:-1] + b',"unexpected":' + extra + b'}')
                    if kind == 'receipt-utf8' and len(args) == 4 and args[:3] == ['bash', 'infra/scripts/lifecycle/lifecycle.sh', 'collect-job']:
                        directory = self.artifacts / args[-1]
                        log = next(directory.glob('*.containers.enrichment-consumer.log'))
                        log.write_bytes(phase.SEED_RECEIPT_MARKER.encode() + b'{"invalid":"\xff"}\n')
                    return result
                with patch.object(phase.Phase, 'run', autospec=True, side_effect=command):
                    self.assertEqual(0, self.main('single-action-seed'))
                self.assertTrue(phase.Phase().state()['seed_gap'])
                self.assertTrue((self.artifacts / 'seed-gap.json').exists())
                self.assertFalse(any(call[0][:2] == ['kubectl', 'create'] for call in self.fake.calls))

    def test_batch_cleanup_deletes_seed_configmap_and_jobs(self):
        self.prepare_live()
        with self.mocked(): self.assertEqual(0, self.main('delete-workload'))
        self.assertEqual(['kubectl', 'delete', 'jobs,configmaps', '-n', 'parchwell', '-l',
            f'app.kubernetes.io/instance={self.run_id}', '--ignore-not-found', '--wait=false', '--request-timeout=30s'], self.fake.calls[0][0])


if __name__ == '__main__':
    unittest.main()

"""Synthetic transport regressions; producer integration has its own required fixture command."""
import base64
import copy
import hashlib
import importlib.util
import json
import os
import subprocess

import yaml
from pathlib import Path
import tempfile
import time
import unittest
from unittest.mock import patch

REPO = Path(__file__).resolve().parents[4]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


phase = module('phase_v2_tests', REPO / 'infra/scripts/phase/phase.py')
v2 = module('v2_transport_tests', REPO / 'infra/scripts/phase/probe_v2.py')
SHA = 'a' * 40
IMAGES = {name: {'digest': 'sha256:' + character * 64, 'reference': f'synthetic.azurecr.io/parchwell/{name}@sha256:' + character * 64}
          for name, character in (('loadgen', 'b'), ('verifier', 'c'))}
RUN = 'probe-synthetic'
NS = '/subscriptions/synthetic/resourceGroups/synthetic/providers/Microsoft.EventHub/namespaces/synthetic'
ACCOUNTS = {name: {'name': name, 'client_id': 'client-' + name, 'principal_id': 'principal-' + name,
                   'subject': 'system:serviceaccount:parchwell:' + name} for name in v2.ACCOUNTS}


def registration(item='B08', configuration='C0', stage='action', account='loadgen'):
    job = v2.job_name(RUN, item, configuration, stage)
    return {'schema_version': 2, 'mode': 'single-action-v2', 'synthetic': True, 'run_id': RUN,
            'scenario_id': item, 'configuration_id': configuration, 'stage': stage, 'source_commit': SHA,
            'job_name': job, 'pod_name': job + '-pod', 'pod_uid': 'synthetic-pod-uid', 'service_account': account}


def capture(identity):
    slots = []
    for ordinal in range(1, 6):
        slots.append({'repeat': ordinal, 'admission': 'not_applicable_no_delivery', 'client_id': None, 'action': 'assign_consume',
            'terminal': False, 'elapsed_ms': 0, 'terminal_unix_ms': None, 'settings': {}, 'omitted_keys': [],
            'metadata_partitions': None, 'requested': None, 'delivery': None,
            'read_observation': {'records_seen': 0, 'eof_count': 0, 'record': None}, 'result': None, 'error': None,
            'client_errors': [], 'statistics': [], 'diagnostics': []})
    return {**identity, 'managed_version': '2.16.0', 'native_version': '2.16.0', 'repeats': slots,
            'handler_exit_code': 0, 'cleanup_completed': True, 'host_cleanup_completed': True}


def pod(record):
    worker = 'verifier' if record['stage'] == 'judge' else 'loadgen'
    return {'metadata': {'name': record['job'] + '-pod', 'uid': 'synthetic-pod-uid',
                         'ownerReferences': [{'controller': True, 'kind': 'Job', 'name': record['job']}],
                         'annotations': {'azure.workload.identity/client-id': 'private-client'}},
            'spec': {'serviceAccountName': record['role'], 'containers': [{'name': record['role'], 'image': IMAGES[worker]['reference'],
                      'env': [{'name': 'AZURE_TENANT_ID', 'value': 'private-tenant'}, {'name': 'AZURE_CLIENT_ID', 'value': 'private-client'},
                              {'name': 'ProbeV2__WorkloadIdentityClientId', 'value': 'private-client'}, {'name': 'AZURE_SUBSCRIPTION_ID', 'value': 'private-subscription'}]}]},
            'status': {'containerStatuses': [{'name': record['role'], 'imageID': IMAGES[worker]['reference'], 'state': {'terminated': {'exitCode': 0}}}]}}


def candidate(item='B08', configuration='C0'):
    outcome = {'result': 'success', 'error_code': None, 'is_fatal': None, 'origin': None}
    repeats = [{'repeat': i, 'status': 'complete_valid', 'reasons': [], 'outcome_class': outcome, 'broker_count': 0, 'statistics_reference': None,
                'semantic_outcome': {'delivered_partition': None, 'consumed_partition': None, 'key_base64': None, 'value_kind': None, 'value_match': None},
                'delivery': None, 'read_observation': None, 'seed_reference': None, 'control_references': [], 'clients': []} for i in range(1, 6)]
    return {'schema_version': 2, 'mode': 'single-action-v2', 'synthetic': True, 'run_id': RUN, 'scenario_id': item,
            'configuration_id': configuration, 'source_commit': SHA, 'managed_version': '2.16.0', 'native_version': '2.16.0',
            'image_digests': {w: v['digest'] for w, v in IMAGES.items()}, 'status': 'complete_valid', 'repeats': repeats,
            'deterministic': True, 'outcome_class': outcome, 'adr012_counts': 'not_applicable', 'diagnostics': ['smoke_only'],
            'security_finding': False, 'item_assessment': None}


class ProbeV2Tests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        root = Path(self.temp.name)
        self.private, self.artifacts, self.candidates = (root / p for p in ('private', 'artifacts', 'candidates'))
        for path in (self.private, self.artifacts, self.candidates, self.private / 'probe-v2'):
            path.mkdir()
        self.environment = patch.dict(os.environ, {'PARCHWELL_PRIVATE_DIRECTORY': str(self.private), 'PARCHWELL_ARTIFACT_DIRECTORY': str(self.artifacts),
            'PARCHWELL_CANDIDATE_DIRECTORY': str(self.candidates), 'PARCHWELL_REPOSITORY': str(REPO), 'PARCHWELL_SCENARIO': 'probe-batch-2',
            'PARCHWELL_RUN_KIND': 'smoke', 'PARCHWELL_BATCH_ITEMS': 'all'})
        self.environment.start()
        self.addCleanup(self.environment.stop)
        self.state = {'phase': 'single-action', 'run_id': RUN, 'modes': [], 'candidate': None, 'safety_stop': False}
        self.write(self.artifacts / 'phase-status.json', self.state)
        self.write(self.private / 'phase.json', {'scenario': 'probe-batch-2', 'run_kind': 'smoke', 'items': list(v2.ITEMS),
            'run_id': RUN, 'source_commit': SHA, 'images': IMAGES, 'phase': 'single-action'})
        self.write(self.private / 'cluster-output.json', {'workload_identity': {'value': {'service_accounts': ACCOUNTS}}})
        self.write(self.private / 'persistent-output.json', {'application_insights_connection_string': {'value': 'synthetic-telemetry'}})
        deployed = {'namespace_id': NS, 'topics': {t: NS + '/eventhubs/' + t for t in v2.TOPICS},
                    'accounts': {a: {k: ACCOUNTS[a][k] for k in ('client_id', 'principal_id')} for a in v2.ACCOUNTS},
                    'assignments': {}, 'probe_namespace_data_roles': True}
        self.write(self.private / 'ephemeral-output.json', {'eventhubs': {'value': {'namespace_name': 'synthetic', 'bootstrap_servers': 'synthetic.servicebus.windows.net:9093', 'topics': {'events': v2.TOPICS[0], 'state': v2.TOPICS[1], 'dlq': v2.TOPICS[2]}}},
            'blob': {'value': {'account_url': 'https://synthetic.blob.core.windows.net', 'containers': {'verifier': 'verifier'}}},
            'private_endpoint_ips': {'value': {'eventhubs': '10.0.0.4', 'blob': '10.0.0.5'}}, 'probe_v2_role_scope': {'value': deployed}})
        self.p = phase.Phase()
        self.subject = v2.ProbeV2(self.p)
        self.calls = []

    @staticmethod
    def write(path, value):
        path.write_text(json.dumps(value))

    def record(self, stage='action', item='B08', configuration='C0', role=None):
        role = role or ('verifier' if stage == 'judge' else 'loadgen')
        return {'job': v2.job_name(RUN, item, configuration, stage), 'item': item, 'configuration': configuration,
                'stage': stage, 'role': role, 'outcome': 'complete', 'collect': 0}

    def evidence(self, record):
        directory = self.artifacts / record['job']
        directory.mkdir(exist_ok=True)
        self.write(directory / 'pods.json', {'items': [pod(record)]})
        ident = registration(record['item'], record['configuration'], record['stage'], record['role'])
        raw_registration = json.dumps(ident, indent=None, separators=(', ', ': ')).encode()
        raw_capture = json.dumps(capture(ident), separators=(',', ':')).encode()
        log = directory / f"{record['job']}-pod.containers.{record['role']}.log"
        log.write_bytes(v2.REGISTRATION_MARKER + raw_registration + b'\n' + v2.CAPTURE_MARKER + raw_capture + b'\n')
        return log, raw_registration, raw_capture

    def transported(self, record):
        payload = {}
        capture_parts = []
        for part in range(4):
            name = record['job'] + '-evidence' + (f'-{part}' if part else '')
            path = self.private / 'probe-v2' / (name + '.json')
            raw = path.read_bytes()
            self.assertLessEqual(len(raw), 1048576)
            manifest = json.loads(raw)
            self.assertTrue(manifest['immutable'])
            self.assertEqual(self.p.read('phase.json')['run_id'], manifest['metadata']['labels']['app.kubernetes.io/instance'])
            self.assertEqual(name, manifest['metadata']['name'])
            self.assertNotIn('data', manifest)
            fields = manifest['binaryData']
            expected = {f'capture.json.part{part}'} | ({'registration.json', 'pod.json'} if part == 0 else set())
            self.assertEqual(expected, set(fields))
            decoded = {key: base64.b64decode(value, validate=True) for key, value in fields.items()}
            capture_parts.append(decoded.pop(f'capture.json.part{part}'))
            self.assertLessEqual(len(capture_parts[-1]), 524288)
            payload.update(decoded)
        payload['capture.json'] = b''.join(capture_parts)
        return payload

    def fake(self, args, seconds, output=None, maximum=4194304):
        self.calls.append((args, seconds, output, maximum))
        if output is not None:
            output.write_text('{}')
        return 0

    def test_selection_preserves_smoke_all_and_accepts_scientific_batch(self):
        for kind, items, valid in [('smoke', 'all', True), ('batch', 'all', True), ('smoke', 'B08', False), ('smoke', '', False), ('unknown', 'all', False)]:
            with self.subTest(kind=kind, items=items), patch.dict(os.environ, PARCHWELL_RUN_KIND=kind, PARCHWELL_BATCH_ITEMS=items):
                if valid:
                    self.assertEqual(0, self.p.validate_batch())
                    with patch.object(self.p, 'run', side_effect=self.fake), patch.object(self.p, 'read', side_effect=AssertionError('no private input')):
                        self.assertEqual(0, self.p.single_action_seed())
                    self.assertEqual([], self.calls)
                else:
                    with self.assertRaisesRegex(phase.Failure, 'unsupported probe batch selection'):
                        self.p.validate_batch()
                    with self.assertRaises(phase.Failure), patch.object(self.p, 'prepare_shared', side_effect=AssertionError('must validate first')):
                        self.p.prepare_batch()

    def batch_phase(self, items=('B08',)):
        current = self.p.read('phase.json')
        current.update(run_kind='batch', items=list(items))
        self.write(self.private / 'phase.json', current)

    def test_every_canonical_batch_selector_and_rejected_alias(self):
        import itertools
        for count in range(1, 4):
            for selected in itertools.combinations(v2.ITEMS, count):
                with patch.dict(os.environ, PARCHWELL_RUN_KIND='batch', PARCHWELL_BATCH_ITEMS=','.join(selected)):
                    self.assertEqual(list(selected), phase.batch_items())
        for selected in ('', 'B08,B08', 'F06,B08', 'B08, E01', 'F04', 'B08,F04', 'b08', 'B08,'):
            with patch.dict(os.environ, PARCHWELL_RUN_KIND='batch', PARCHWELL_BATCH_ITEMS=selected):
                with self.assertRaises(phase.Failure):
                    phase.batch_items()

    def test_selected_values_use_baseline_roles_only_for_partition_probe(self):
        for items, roles in [(('B08',), False), (('E01',), True), (('F06',), True), (('B08', 'F06'), True)]:
            self.batch_phase(items)
            item = items[0]
            for stage, account, _ in v2.stages(item, 'C0'):
                runtime = self.subject.values(item, 'C0', stage, account)['runtime']
                self.assertEqual('batch', runtime['run_kind'])
                self.assertIs(roles, runtime['probe_namespace_data_roles'])

    def test_batch_assessment_shape_final_mode_and_closed_fields(self):
        value = candidate('B08', 'C1')
        value['diagnostics'] = ['full_experiment']
        assessment = {'verdict': 'confirmed', 'hypotheses': {'H_partition': 'holds'}, 'reason': None}
        value['item_assessment'] = assessment
        self.assertTrue(self.subject.candidate_shape(value, 'batch'))
        self.assertFalse(self.subject.candidate_shape(value, 'smoke'))
        for altered in [dict(assessment, proof=True), dict(assessment, hypotheses={'other': 'holds'}),
                        dict(assessment, verdict='success'), dict(assessment, reason=3)]:
            value['item_assessment'] = altered
            self.assertFalse(self.subject.candidate_shape(value, 'batch'))
        value['item_assessment'] = assessment
        value['configuration_id'] = 'C0'
        self.assertFalse(self.subject.candidate_shape(value, 'batch'))

    def test_submission_freshness_is_checked_after_real_render_and_consumed_once(self):
        self.batch_phase(('F06',))
        for elapsed, permitted in [(59.999, True), (60, True), (60.001, False), (-0.001, False)]:
            self.write(self.artifacts / 'phase-status.json', dict(self.state, modes=[], permission_audit={
                'status': 'complete', 'run_id': RUN, 'clock': 'monotonic', 'boot_identity': 'synthetic-boot',
                'completed_monotonic': 100, 'projection_sha256': 'a' * 64, 'first_submission_consumed': False}))
            current = [100]
            calls = []
            def invoke(args, seconds, output=None, maximum=4194304):
                calls.append(args)
                if args[0] == 'helm':
                    output.write_text('kind: Job\n  name: "' + v2.job_name(RUN, 'F06', 'C6', 'seed') + '"\n  activeDeadlineSeconds: 1980\n')
                    current[0] = 100 + elapsed
                return 0
            with self.subTest(elapsed=elapsed), patch.object(v2.time, 'monotonic', side_effect=lambda: current[0]), patch.object(self.p, 'boot_identity', return_value='synthetic-boot'), patch.object(self.p, 'run', side_effect=invoke), patch.object(self.subject, 'wait', return_value=0), patch.object(self.subject, 'collect', return_value=(0, True)):
                self.assertTrue(self.subject.access_admission())
                record = self.subject.run_stage('F06', 'C6', 'seed', 'loadgen', 1980, 5000)
                self.assertEqual(permitted, any(c[:2] == ['kubectl', 'apply'] for c in calls))
                if permitted:
                    audit = self.p.state()['permission_audit']
                    self.assertTrue(audit['first_submission_consumed'])
                    self.assertAlmostEqual(elapsed * 1000, audit['submission_age_ms'])
                    current[0] = 10000
                    self.assertTrue(self.subject.access_admission())
                else:
                    self.assertEqual(('not_attempted', 'permission_evidence_incomplete'), (record['outcome'], record['reason']))
                    self.assertFalse(self.subject.access_admission())

    def test_permission_clock_and_binding_gaps_block_admission(self):
        self.batch_phase(('F06',))
        audit = {'status': 'complete', 'run_id': RUN, 'clock': 'monotonic', 'boot_identity': 'synthetic-boot',
                 'completed_monotonic': 100, 'projection_sha256': 'a' * 64}
        for change in ({'run_id': 'wrong'}, {'clock': 'wall'}, {'boot_identity': None}, {'boot_identity': 'other'},
                       {'completed_monotonic': True}, {'completed_monotonic': '100'}, {'status': 'mismatch'}, {'status': 'incomplete'}, {'projection_sha256': 'bad'}):
            self.write(self.artifacts / 'phase-status.json', dict(self.state, permission_audit=dict(audit, **change)))
            with patch.object(v2.time, 'monotonic', return_value=101), patch.object(self.p, 'boot_identity', return_value='synthetic-boot'):
                self.assertFalse(self.subject.access_admission())
                self.assertTrue(self.p.state()['permission_audit']['blocked'])

    def test_matrix_counts_and_runtime_are_fixed_for_every_stage(self):
        counts = {item: [] for item in v2.ITEMS}
        for item, configuration in v2.CONFIGURATIONS:
            for stage, role, seconds in v2.stages(item, configuration):
                counts[item].append(stage)
                runtime = self.subject.values(item, configuration, stage, role)['runtime']
                self.assertFalse(runtime['image_smoke'])
                self.assertTrue(runtime['probe_namespace_data_roles'])
                self.assertEqual(role, runtime['action_role'])
                self.assertEqual(stage, runtime['stage'])
                self.assertEqual(set(v2.ACCOUNTS), set(runtime['service_accounts']))
                self.assertEqual('single-action-v2-judge' if stage == 'judge' else 'single-action-v2', runtime['mode'])
                self.assertEqual(self.subject.limits, runtime['bounds']['probe_v2'])
                for forbidden in ('enrichment_image', 'repeat', 'seed_receipt_configmap', 'loadgen_job_name'):
                    self.assertNotIn(forbidden, runtime)
                self.assertIn(seconds, (120, 900, 1980))
        self.assertEqual({'B08': 6, 'E01': 10, 'F06': 33}, {k: len(v) for k, v in counts.items()})
        self.assertEqual(165, sum(stage != 'judge' for values in counts.values() for stage in values) * 5)
        with self.assertRaises(ValueError):
            self.subject.values('B08', 'C0', 'action', 'probe')

    def test_actual_scientific_phase_values_render_all_stages_through_helm(self):
        self.batch_phase(v2.ITEMS)
        self.test_actual_phase_values_render_all_stages_through_helm()

    def test_actual_phase_values_render_all_stages_through_helm(self):
        runtime = yaml.safe_load((REPO / 'charts/parchwell/tests/fixture.yaml').read_text())['runtime']
        state = self.p.read('phase.json')
        state['source_commit'] = runtime['source_commit']
        state['images'] = {w: {'reference': runtime[w + '_image'], 'digest': runtime[w + '_image'].rsplit('@', 1)[1]} for w in ('loadgen', 'verifier')}
        self.write(self.private / 'phase.json', state)
        ephemeral = self.p.read('ephemeral-output.json')
        for key in ('eventhubs', 'blob'):
            ephemeral[key] = {'value': runtime[key]}
        ephemeral['blob']['value']['containers']['single_action'] = 'single-action'
        self.write(self.private / 'ephemeral-output.json', ephemeral)
        accounts = {a: {'name': a, 'client_id': f'00000000-0000-0000-0000-{i + 1:012d}', 'subject': 'system:serviceaccount:parchwell:' + a} for i, a in enumerate(v2.ACCOUNTS)}
        self.write(self.private / 'cluster-output.json', {'workload_identity': {'value': {'service_accounts': accounts}}})
        self.write(self.private / 'persistent-output.json', {'application_insights_connection_string': {'value': runtime['application_insights_connection_string']}})
        rendered_count = 0
        for item, configuration in v2.CONFIGURATIONS:
            for stage, account, seconds in v2.stages(item, configuration):
                with self.subTest(item=item, configuration=configuration, stage=stage):
                    values = self.subject.values(item, configuration, stage, account)
                    path = self.private / 'v2-chart-values.json'; self.write(path, values)
                    result = subprocess.run(['helm', 'template', 'parchwell', 'charts/parchwell', '--values', str(path)], cwd=REPO, capture_output=True, text=True, timeout=60)
                    self.assertEqual(0, result.returncode, result.stderr)
                    objects = list(yaml.safe_load_all(result.stdout))
                    self.assertEqual(4, sum(o['kind'] == 'ServiceAccount' for o in objects))
                    jobs = [o for o in objects if o['kind'] == 'Job']; self.assertEqual(1, len(jobs))
                    job = jobs[0]
                    self.assertEqual(v2.job_name(RUN, item, configuration, stage), job['metadata']['name'])
                    self.assertEqual(seconds, job['spec']['activeDeadlineSeconds'])
                    self.assertEqual(account, job['spec']['template']['spec']['serviceAccountName'])
                    container = job['spec']['template']['spec']['containers'][0]
                    self.assertEqual(['--single-action-v2-judge' if stage == 'judge' else '--single-action-v2'], container['args'])
                    prefix = 'ProbeV2Judge__' if stage == 'judge' else 'ProbeV2__'
                    env = {e['name']: e for e in container['env']}
                    if stage == 'judge':
                        self.assertEqual(values['runtime']['run_kind'], env['ProbeV2Judge__RunKind']['value'])
                    else:
                        self.assertNotIn('ProbeV2__RunKind', env)
                        self.assertNotIn('ProbeV2Judge__RunKind', env)
                    self.assertEqual(runtime['eventhubs']['namespace_name'], env[prefix + 'NamespaceName']['value'])
                    self.assertEqual(accounts[account]['client_id'], env[prefix + 'WorkloadIdentityClientId']['value'])
                    rendered_count += 1
        self.assertEqual(49, rendered_count)

    def test_scope_is_private_bound_and_created_immutably_once(self):
        confirmation = {'schema_version': 2, 'namespace_id': NS,
            'accounts': {a: {'principal_id': ACCOUNTS[a]['principal_id'], 'no_additional_data_access': True} for a in ('loadgen', 'enrichment-consumer')}}
        raw = json.dumps(confirmation, indent=2).encode()
        (self.private / 'probe-v2/effective-scope.json').write_bytes(raw)
        with patch.object(self.p, 'run', side_effect=self.fake):
            self.assertTrue(self.subject.scope())
            with self.assertRaisesRegex(ValueError, 'already_materialized'):
                self.subject.scope()
        snapshot = json.loads((self.private / 'probe-v2/role-scope.json').read_text())
        self.assertEqual(hashlib.sha256(raw).hexdigest(), snapshot['effective_scope_sha256'])
        self.assertEqual('synthetic', snapshot['namespace_name'])
        self.assertEqual(confirmation, snapshot['effective_scope'])
        manifest = json.loads((self.private / f'probe-v2/{RUN}-v2-role-scope.json').read_text())
        self.assertTrue(manifest['immutable'])
        self.assertEqual({'role-scope.json'}, set(manifest['data']))
        self.assertNotIn('binaryData', manifest)
        self.assertEqual([], list(self.artifacts.glob('*scope*')))
        self.assertEqual('create', self.calls[0][0][1])

    def test_missing_or_false_confirmation_stays_a_gap(self):
        for invalid in (None, {'schema_version': 2, 'namespace_id': NS, 'accounts': {'loadgen': {'principal_id': ACCOUNTS['loadgen']['principal_id'], 'no_additional_data_access': False}, 'enrichment-consumer': {'principal_id': ACCOUNTS['enrichment-consumer']['principal_id'], 'no_additional_data_access': True}}}):
            with self.subTest(invalid=invalid):
                if invalid is not None:
                    self.write(self.private / 'probe-v2/effective-scope.json', invalid)
                (self.private / f'probe-v2/{RUN}-v2-role-scope.json').unlink(missing_ok=True)
                with patch.object(self.p, 'run', side_effect=self.fake):
                    self.assertTrue(self.subject.scope())
                snapshot = json.loads((self.private / 'probe-v2/role-scope.json').read_text())
                self.assertIsNone(snapshot['effective_scope'])
                self.assertIsNone(snapshot['effective_scope_sha256'])

    def test_superseded_or_commit_bound_confirmation_stays_a_gap(self):
        accounts = {a: {'principal_id': ACCOUNTS[a]['principal_id'], 'no_additional_data_access': True} for a in ('loadgen', 'enrichment-consumer')}
        for invalid in ({'schema_version': 1, 'source_commit': SHA, 'namespace_id': NS, 'accounts': accounts},
                        {'schema_version': 2, 'source_commit': SHA, 'namespace_id': NS, 'accounts': accounts},
                        {'schema_version': 3, 'namespace_id': NS, 'accounts': accounts}):
            with self.subTest(keys=sorted(invalid), version=invalid['schema_version']):
                self.write(self.private / 'probe-v2/effective-scope.json', invalid)
                (self.private / f'probe-v2/{RUN}-v2-role-scope.json').unlink(missing_ok=True)
                with patch.object(self.p, 'run', side_effect=self.fake):
                    self.assertTrue(self.subject.scope())
                snapshot = json.loads((self.private / 'probe-v2/role-scope.json').read_text())
                self.assertIsNone(snapshot['effective_scope'])
                self.assertIsNone(snapshot['effective_scope_sha256'])

    def test_transfer_preserves_exact_payload_bytes_and_never_replaces(self):
        record = self.record()
        _, registration_bytes, capture_bytes = self.evidence(record)
        with patch.object(self.p, 'run', side_effect=self.fake):
            self.assertTrue(self.subject.transfer(record))
            self.assertFalse(self.subject.transfer(record))
        payload = self.transported(record)
        self.assertEqual(registration_bytes, payload['registration.json'])
        self.assertEqual(capture_bytes, payload['capture.json'])
        self.assertEqual({'registration.json', 'capture.json', 'pod.json'}, set(payload))
        self.assertEqual(4, len(self.calls))

    def test_five_full_statistics_reports_roundtrip_above_one_mib_without_byte_or_hash_changes(self):
        record = self.record()
        log, reg, cap = self.evidence(record)
        value = json.loads(cap)
        report = b'{"synthetic":true,"padding":"' + b'x' * (262144 - 31) + b'"}'
        self.assertEqual(262144, len(report))
        for slot in value['repeats']:
            slot['statistics'] = [{'sequence': 1, 'observed_elapsed_ms': 1, 'raw_base64': base64.b64encode(report).decode()}]
        raw = json.dumps(value, ensure_ascii=False, separators=(',', ':')).encode()
        self.assertGreater(len(raw), 1048576)
        source_log = v2.REGISTRATION_MARKER + reg + b'\n' + v2.CAPTURE_MARKER + raw + b'\n'
        self.assertGreater(len(source_log), 1048576)
        log.unlink()
        (log.parent / 'pods.json').unlink()
        maximum_log = self.subject.limits['execution']['max_container_log_bytes']
        def collect_command(args, seconds, output=None, maximum=4194304):
            if args[1:3] == ['get', 'pods']:
                self.write(output, {'items': [pod(record)]})
                return 0
            self.assertEqual(['kubectl', 'logs', '-n', 'parchwell', record['job'] + '-pod', '-c', record['role'], '--timestamps=false', f'--limit-bytes={maximum_log + 1}', '--request-timeout=30s'], args)
            self.assertEqual(maximum_log, maximum)
            output.write_bytes(source_log[:maximum])
            return 125 if len(source_log) > maximum else 0
        with patch.object(self.p, 'run', side_effect=collect_command):
            self.assertEqual((0, True), self.subject.collect(record))
        self.assertEqual(source_log, log.read_bytes())
        self.assertTrue(json.loads((log.parent / 'collection-status.json').read_bytes())['complete'])
        with patch.object(self.p, 'run', side_effect=self.fake):
            self.assertTrue(self.subject.transfer(record))
        result = self.transported(record)
        self.assertEqual(raw, result['capture.json'])
        self.assertEqual(hashlib.sha256(raw).digest(), hashlib.sha256(result['capture.json']).digest())
        self.assertEqual(reg, result['registration.json'])
        self.assertEqual(4, len(self.calls))

    def test_collection_log_limit_is_inclusive_and_truncation_reports_safe_bound_details(self):
        maximum_log = self.subject.limits['execution']['max_container_log_bytes']
        for offset in (-1, 0, 1):
            with self.subTest(offset=offset):
                record = self.record(configuration=f'C{offset + 1}')
                source = b'x' * (maximum_log + offset)
                def collect_command(args, seconds, output=None, maximum=4194304):
                    if args[1:3] == ['get', 'pods']:
                        self.write(output, {'items': [pod(record)]})
                        return 0
                    self.assertEqual(['kubectl', 'logs', '-n', 'parchwell', record['job'] + '-pod', '-c', record['role'], '--timestamps=false', f'--limit-bytes={maximum_log + 1}', '--request-timeout=30s'], args)
                    self.assertEqual(maximum_log, maximum)
                    output.write_bytes(source[:maximum])
                    return 125 if len(source) > maximum else 0
                with patch.object(self.p, 'run', side_effect=collect_command):
                    self.assertEqual((int(offset > 0), True), self.subject.collect(record))
                status = json.loads((self.artifacts / record['job'] / 'collection-status.json').read_bytes())
                self.assertEqual(offset <= 0, status['complete'])
                self.assertEqual([] if offset <= 0 else [{'kind': 'truncated_log', 'status': 125, 'limit': 'container_log_bytes', 'observed': maximum_log + 1, 'observed_at_least': True, 'allowed': maximum_log}], status['gaps'])

    def test_first_binary_configmap_fits_existing_serialized_bound_at_all_decoded_maxima(self):
        fields = {'registration.json': b'x' * 65536, 'pod.json': b'x' * 65536, 'capture.json.part0': b'x' * 524288}
        with patch.object(self.p, 'run', side_effect=self.fake):
            self.assertTrue(self.subject.configmap('synthetic-max-evidence', {name: base64.b64encode(raw).decode() for name, raw in fields.items()}, binary=True))
        raw = (self.private / 'probe-v2/synthetic-max-evidence.json').read_bytes()
        self.assertLessEqual(len(raw), 1048576)
        self.assertEqual(fields, {name: base64.b64decode(value, validate=True) for name, value in json.loads(raw)['binaryData'].items()})

    def test_utf8_bytes_remain_exact_and_invalid_utf8_has_no_lossy_fallback(self):
        for valid in (True, False):
            with self.subTest(valid=valid):
                record = self.record(configuration='C0' if valid else 'C1')
                log, reg, cap = self.evidence(record)
                value = json.loads(cap)
                value['managed_version'] += '-π'
                raw = json.dumps(value, ensure_ascii=False).encode()
                if not valid:
                    raw = raw.replace('π'.encode(), b'\xff')
                log.write_bytes(v2.REGISTRATION_MARKER + reg + b'\n' + v2.CAPTURE_MARKER + raw + b'\n')
                with patch.object(self.p, 'run', side_effect=self.fake):
                    self.assertEqual(valid, self.subject.transfer(record))
                if valid:
                    self.assertEqual(raw, self.transported(record)['capture.json'])
                else:
                    self.assertFalse((self.private / f"probe-v2/{record['job']}-evidence.json").exists())

    def test_oversize_statistics_reports_include_safe_bound_details(self):
        record = self.record()
        log, reg, cap = self.evidence(record)
        value = json.loads(cap)
        maximum = self.subject.limits['capture']['MaxStatisticsReportBytes']
        value['repeats'][0]['statistics'] = [{'sequence': 1, 'observed_elapsed_ms': 1, 'raw_base64': base64.b64encode(b'x' * (maximum + 1)).decode()}]
        log.write_bytes(v2.REGISTRATION_MARKER + reg + b'\n' + v2.CAPTURE_MARKER + json.dumps(value).encode() + b'\n')
        with patch.object(self.p, 'run', side_effect=self.fake):
            self.assertFalse(self.subject.transfer(record))
        gap = json.loads((self.artifacts / record['job'] / 'v2-transfer-gap.json').read_bytes())
        self.assertEqual({'job': record['job'], 'reason': 'v2_evidence_transport', 'limit': 'statistics_report_bytes', 'observed': maximum + 1, 'allowed': maximum}, gap)
        self.assertEqual([], self.calls)

    def test_capture_aggregate_limit_is_inclusive_and_safe_rejection_names_bound(self):
        for offset in (-1, 0, 1):
            with self.subTest(offset=offset):
                record = self.record(configuration=f'C{offset + 1}')
                log, reg, cap = self.evidence(record)
                maximum = self.subject.limits['capture']['MaxCaptureBytes']
                raw = cap + b' ' * (maximum + offset - len(cap))
                log.write_bytes(v2.REGISTRATION_MARKER + reg + b'\n' + v2.CAPTURE_MARKER + raw + b'\n')
                with patch.object(self.p, 'run', side_effect=self.fake):
                    self.assertEqual(offset <= 0, self.subject.transfer(record))
                if offset <= 0:
                    self.assertEqual(raw, self.transported(record)['capture.json'])
                else:
                    gap = json.loads((self.artifacts / record['job'] / 'v2-transfer-gap.json').read_bytes())
                    self.assertEqual({'job': record['job'], 'reason': 'v2_evidence_transport', 'limit': 'capture.json_bytes', 'observed': maximum + 1, 'allowed': maximum}, gap)

    def test_transfer_rejects_owner_image_marker_order_duplicates_and_wrong_receipt(self):
        record = self.record()
        mutations = ('owner', 'image', 'duplicates', 'order', 'identity', 'slot', 'unknown')
        for mutation in mutations:
            with self.subTest(mutation=mutation):
                log, reg, cap = self.evidence(record)
                path = self.artifacts / record['job'] / 'pods.json'
                data = json.loads(path.read_text())
                if mutation == 'owner':
                    data['items'][0]['metadata']['ownerReferences'][0]['name'] = 'other-job'
                elif mutation == 'image':
                    data['items'][0]['status']['containerStatuses'][0]['imageID'] = IMAGES['verifier']['reference']
                elif mutation == 'duplicates':
                    log.write_bytes(log.read_bytes() + v2.CAPTURE_MARKER + cap + b'\n')
                elif mutation == 'order':
                    log.write_bytes(v2.CAPTURE_MARKER + cap + b'\n' + v2.REGISTRATION_MARKER + reg + b'\n')
                else:
                    value = json.loads(cap)
                    if mutation == 'identity': value['pod_uid'] = 'other-pod'
                    if mutation == 'slot': value['repeats'][1]['repeat'] = 1
                    if mutation == 'unknown': value['new_field'] = True
                    log.write_bytes(v2.REGISTRATION_MARKER + reg + b'\n' + v2.CAPTURE_MARKER + json.dumps(value).encode() + b'\n')
                self.write(path, data)
                with patch.object(self.p, 'run', side_effect=self.fake):
                    self.assertFalse(self.subject.transfer(record))
                self.assertEqual([], self.calls)

    def test_capture_diagnostic_completion_false_transports_without_becoming_success(self):
        record = self.record()
        log, reg, cap = self.evidence(record)
        value = json.loads(cap)
        value['handler_exit_code'] = 1
        value['cleanup_completed'] = False
        log.write_bytes(v2.REGISTRATION_MARKER + reg + b'\n' + v2.CAPTURE_MARKER + json.dumps(value).encode() + b'\n')
        with patch.object(self.p, 'run', side_effect=self.fake):
            self.assertTrue(self.subject.transfer(record))
        # Worker failure is retained for the verifier; transport never upgrades its cleanup flags.
        self.assertFalse(json.loads(self.transported(record)['capture.json'])['cleanup_completed'])
        self.assertFalse(record['capture_complete'])

    def candidate_run(self, value=None, mutate_receipt=None, mutate_pod=None):
        value = value or candidate()
        record = self.record('judge', value['scenario_id'], value['configuration_id'])
        self.evidence(record)
        if mutate_pod:
            path = self.artifacts / record['job'] / 'pods.json'
            data = json.loads(path.read_text()); mutate_pod(data); self.write(path, data)
        raw = json.dumps(value).encode()
        receipt = {'schema_version': 2, 'mode': 'single-action-v2-judge', 'run_id': RUN, 'scenario_id': record['item'], 'configuration_id': record['configuration'],
                   'source_commit': SHA, 'job_name': record['job'], 'pod_name': record['job'] + '-pod', 'pod_uid': 'synthetic-pod-uid',
                   'candidate_name': f"{RUN}/single-action-v2/{record['item']}/{record['configuration']}/candidate.json", 'candidate_sha256': hashlib.sha256(raw).hexdigest(),
                   'handler_exit_code': 0, 'cleanup_completed': True}
        if mutate_receipt:
            mutate_receipt(receipt)
        payloads = iter((raw, json.dumps(receipt).encode()))
        def download(name, target, maximum, deadline):
            value = next(payloads); target.write_bytes(value); return value
        with patch.object(self.p, 'download_blob', side_effect=download):
            return self.subject.candidate(record, time.monotonic() + 120)

    def test_candidate_hash_identity_and_exact_two_images_bind_to_owning_pod(self):
        self.assertTrue(self.candidate_run())
        mutations = [lambda r: r.update(candidate_sha256='0' * 64), lambda r: r.update(cleanup_completed=False),
                     lambda r: r.update(pod_uid='wrong'), lambda r: r.update(candidate_name='other/candidate.json')]
        for mutation in mutations:
            with self.subTest(mutation=mutation):
                self.assertFalse(self.candidate_run(mutate_receipt=mutation))
        for images in ({'loadgen': IMAGES['loadgen']['digest']}, {**{w: v['digest'] for w, v in IMAGES.items()}, 'enrichment': 'sha256:' + 'd' * 64},
                       {'loadgen': IMAGES['verifier']['digest'], 'verifier': IMAGES['loadgen']['digest']}):
            value = candidate(); value['image_digests'] = images
            self.assertFalse(self.candidate_run(value))
        self.assertFalse(self.candidate_run(mutate_pod=lambda p: p['items'][0]['metadata']['ownerReferences'][0].update(name='other')))

    def test_smoke_gate_uses_verifier_flags_without_scientific_rejudgement(self):
        for field, value in [('status', 'incomplete'), ('deterministic', False), ('security_finding', True), ('diagnostics', ['smoke_only', 'smoke_controls_failed'])]:
            document = candidate(); document[field] = value
            self.assertFalse(self.candidate_run(document))
            self.assertTrue(self.p.state()['candidates'][-1]['accepted'])
        document = candidate(); document['item_assessment'] = {'verdict': 'confirmed', 'hypotheses': {}, 'reason': None}
        self.assertFalse(self.candidate_run(document))
        self.assertFalse(self.p.state()['candidates'][-1]['accepted'])

    def test_batch_transport_retains_diagnostic_gaps_and_accepts_contrary_science(self):
        self.batch_phase()
        for verdict in ('confirmed', 'contradicted', 'untestable'):
            value = candidate('B08', 'C1')
            value['diagnostics'] = ['full_experiment']
            value['item_assessment'] = {'verdict': verdict, 'hypotheses': {'H_partition': 'undecided'}, 'reason': None}
            self.assertTrue(self.candidate_run(value))
            self.assertTrue(self.p.state()['candidates'][-1]['accepted'])
            self.assertFalse(self.p.state()['candidates'][-1]['smoke_valid'])
        value['item_assessment']['verdict'] = None
        self.assertFalse(self.candidate_run(value))
        self.assertTrue(self.p.state()['candidates'][-1]['accepted'])
        for diagnostics in (['smoke_only'], ['smoke_only', 'full_experiment'], ['full_experiment', 'full_experiment'], ['full_experiment', 'smoke_controls_failed']):
            value['diagnostics'] = diagnostics
            self.assertFalse(self.candidate_run(value))
            self.assertFalse(self.p.state()['candidates'][-1]['accepted'])

    def test_batch_permission_audit_reference_binds_exact_artifact_bytes(self):
        self.batch_phase(('F06',))
        raw = b'{"synthetic":"projection"}'
        (self.artifacts / 'probe-v2-access-check.json').write_bytes(raw)
        digest = hashlib.sha256(raw).hexdigest()
        value = candidate('F06', 'C11')
        value['diagnostics'] = ['full_experiment', 'permission_audit_sha256:' + digest]
        value['item_assessment'] = {'verdict': 'undocumented', 'hypotheses': {'H_positive_scope': 'holds', 'H_negative_surface': 'recorded_only'}, 'reason': None}
        self.assertTrue(self.candidate_run(value))
        for references in ([], ['permission_audit_sha256:' + '0' * 64], ['permission_audit_sha256:bad'], ['permission_audit_sha256:' + digest] * 2):
            value['diagnostics'] = ['full_experiment'] + references
            self.assertFalse(self.candidate_run(value))
        value['item_assessment'] = {'verdict': None, 'hypotheses': {'H_positive_scope': 'undecided', 'H_negative_surface': 'recorded_only'}, 'reason': 'incomplete_configuration'}
        value['diagnostics'] = ['full_experiment', 'v2_access_evidence']
        self.assertFalse(self.candidate_run(value))
        self.assertTrue(self.p.state()['candidates'][-1]['accepted'])

    def test_public_pod_redacts_identity_values_preserving_coordinates(self):
        value = pod(self.record())
        value['spec']['containers'][0]['env'].extend([{'name': 'Telemetry__ConnectionString', 'value': 'private-telemetry'}, {'name': 'APPLICATIONINSIGHTS_CONNECTION_STRING', 'value': 'private-app-insights'}])
        value['spec']['initContainers'] = [{'env': [{'name': 'ARM_SUBSCRIPTION_ID', 'value': 'private-subscription'}, {'name': 'ProbeV2Judge__PrincipalId', 'value': 'private-principal'}]}]
        v2.public_pod(value)
        raw = json.dumps(value)
        for forbidden in ('private-client', 'private-tenant', 'private-subscription', 'private-principal', 'private-telemetry', 'private-app-insights'):
            self.assertNotIn(forbidden, raw)
        self.assertEqual('synthetic-pod-uid', value['metadata']['uid'])
        self.assertEqual(IMAGES['loadgen']['reference'], value['status']['containerStatuses'][0]['imageID'])
        self.assertEqual('loadgen', value['spec']['serviceAccountName'])

    def test_closed_json_rejects_depth_duplicate_and_size_boundaries(self):
        self.assertEqual({'x': 1}, v2.decode(b'{"x":1}', 7))
        for raw, limit in [(b'{"x":1}', 6), (b'{"x":1,"x":2}', 32), (b'[' * 33 + b'0' + b']' * 33, 128), (b'{"x":NaN}', 32), (b'', 32), (b'{} trailing', 32)]:
            with self.subTest(raw=raw), self.assertRaises(ValueError):
                v2.decode(raw, limit)

    def test_real_stage_transport_is_bounded_and_redacts_public_identity(self):
        record = self.record()
        identity = registration()
        def command(args, seconds, output=None, maximum=4194304):
            self.calls.append((args, seconds, output, maximum))
            if args[0] == 'helm':
                output.write_text(f'kind: Job\nmetadata:\n  name: "{record["job"]}"\nspec:\n  activeDeadlineSeconds: 1980\n')
            elif args[:3] == ['kubectl', 'get', 'job']:
                self.write(output, {'status': {'conditions': [{'type': 'Complete', 'status': 'True'}]}})
            elif args[:3] == ['kubectl', 'get', 'pods']:
                self.write(output, {'items': [pod(record)]})
            elif args[:2] == ['kubectl', 'logs']:
                output.write_bytes(v2.REGISTRATION_MARKER + json.dumps(identity).encode() + b'\n' + v2.CAPTURE_MARKER + json.dumps(capture(identity)).encode() + b'\n')
            return 0
        with patch.object(self.p, 'run', side_effect=command):
            result = self.subject.run_stage('B08', 'C0', 'action', 'loadgen', 1980, time.monotonic() + 2100)
            self.assertTrue(self.subject.transfer(result))
        self.assertEqual('complete', result['outcome'])
        self.assertEqual(0, result['collect'])
        self.assertTrue(all(seconds <= 60 for _, seconds, _, _ in self.calls))
        exported = (self.artifacts / record['job'] / 'pods.json').read_text()
        self.assertNotIn('private-client', exported)
        self.assertNotIn('private-tenant', exported)
        private = (self.private / 'probe-v2' / (record['job'] + '-pods.json')).read_text()
        self.assertIn('private-client', private)

    def test_uncertain_job_cleanup_stops_every_later_admission(self):
        record = self.record()
        def command(args, seconds, output=None, maximum=4194304):
            self.calls.append((args, seconds, output, maximum))
            if args[0] == 'helm':
                output.write_text(f'kind: Job\nmetadata:\n  name: "{record["job"]}"\nspec:\n  activeDeadlineSeconds: 1980\n')
            elif args[:2] == ['kubectl', 'apply']:
                return 1
            elif args[:3] == ['kubectl', 'get', 'pods']:
                self.write(output, {'items': [pod(record)]})
            return 0
        with patch.object(self.p, 'run', side_effect=command), patch.object(self.p, 'sleep'):
            result = self.subject.run_stage('B08', 'C0', 'action', 'loadgen', 1980, time.monotonic() + 2100)
            count = len(self.calls)
            later = self.subject.run_stage('B08', 'C0', 'read', 'probe', 900, time.monotonic() + 1020)
        self.assertEqual('apply_failed', result['outcome'])
        self.assertTrue(self.p.state()['safety_stop'])
        self.assertEqual('not_attempted', later['outcome'])
        self.assertEqual('safety_stop', later['reason'])
        self.assertEqual(count, len(self.calls))
        self.assertEqual(18, sum(args[:3] == ['kubectl', 'get', 'pods'] for args, _, _, _ in self.calls))

    def test_rendered_deadline_mismatch_never_applies(self):
        record = self.record()
        def command(args, seconds, output=None, maximum=4194304):
            self.calls.append((args, seconds, output, maximum))
            output.write_text(f'kind: Job\nmetadata:\n  name: "{record["job"]}"\nspec:\n  activeDeadlineSeconds: 1979\n')
            return 0
        with patch.object(self.p, 'run', side_effect=command):
            result = self.subject.run_stage('B08', 'C0', 'action', 'loadgen', 1980, time.monotonic() + 2100)
        self.assertEqual('render_failed', result['outcome'])
        self.assertEqual(1, len(self.calls))
        self.assertIsNone(result['apply'])

    def test_all_stages_have_receipts_even_when_dependencies_fail(self):
        def run_stage(item, configuration, stage, role, seconds, deadline, skip):
            record = self.record(stage, item, configuration, role)
            state = self.p.state(); state['modes'].append(record); self.p.save_state(state)
            return record
        with patch.object(self.subject, 'scope', return_value=True), patch.object(self.p, 'admission_allowed', return_value=True), \
             patch.object(self.subject, 'run_stage', side_effect=run_stage), patch.object(self.subject, 'transfer', return_value=False) as transfer, \
             patch.object(self.subject, 'candidate', return_value=True):
            self.assertEqual(1, self.subject.run_batch())
        self.assertEqual(49, len(self.p.state()['modes']))
        self.assertEqual(33, transfer.call_count)
        report = json.loads((self.artifacts / 'probe-v2-smoke.json').read_text())
        self.assertEqual(80, report['action_slots'])
        self.assertEqual(165, report['maximum_clients'])
        self.assertFalse(report['evidence_passed'])

    def test_batch_selected_stage_counts_do_not_schedule_other_items(self):
        import itertools
        counts = {'B08': (6, 10, 20), 'E01': (10, 10, 40), 'F06': (33, 60, 105)}
        for count in range(1, 4):
            for items in itertools.combinations(v2.ITEMS, count):
                self.batch_phase(items)
                self.write(self.artifacts / 'phase-status.json', dict(self.state, modes=[]))
                def run_stage(item, configuration, stage, role, seconds, deadline, skip):
                    record = self.record(stage, item, configuration, role)
                    if stage != 'judge':
                        record.update(capture_complete=True, admitted_client_slots=5)
                    state = self.p.state(); state['modes'].append(record); self.p.save_state(state)
                    return record
                def audit(_deadline):
                    state = self.p.state()
                    state['permission_audit'] = {'status': 'complete'}
                    self.p.save_state(state)
                    return 'complete'
                with self.subTest(items=items), patch.object(self.subject, 'scope', return_value=True), patch.object(self.p, 'admission_allowed', return_value=True), patch.object(self.subject, 'run_stage', side_effect=run_stage), patch.object(self.subject, 'transfer', return_value=True), patch.object(self.subject, 'candidate', return_value=True), patch.object(self.subject, 'collect_access', side_effect=audit) as collector, patch.object(self.subject, 'access_admission', return_value=True):
                    self.assertEqual(0, self.subject.run_batch())
                    self.assertEqual(int('F06' in items), collector.call_count)
                report = json.loads((self.artifacts / 'probe-v2-batch.json').read_text())
                self.assertEqual(sum(counts[i][0] for i in items), report['jobs'])
                self.assertEqual(sum(counts[i][1] for i in items), report['action_slots'])
                self.assertEqual(sum(counts[i][2] for i in items), report['maximum_clients'])
                self.assertEqual(report['maximum_clients'], report['admitted_client_slots'])
                self.assertEqual([i for i in v2.ITEMS if i not in items], self.p.state()['not_selected'])
                self.assertEqual(set(items), {r['item'] for r in self.p.state()['modes']})

    def test_access_gap_or_mismatch_blocks_every_f06_client_but_keeps_judges(self):
        self.batch_phase(('F06',))
        for status, reason in [('incomplete', 'permission_evidence_incomplete'), ('mismatch', 'permission_configuration_mismatch')]:
            self.write(self.artifacts / 'phase-status.json', dict(self.state, modes=[]))
            def audit(_deadline):
                state = self.p.state(); state['permission_audit'] = {'status': status}; self.p.save_state(state)
                return status
            with patch.object(self.subject, 'scope', return_value=True), patch.object(self.subject, 'collect_access', side_effect=audit) as collector, patch.object(self.subject, 'run_stage', wraps=self.subject.run_stage) as runner, patch.object(self.p, 'run', side_effect=self.fake), patch.object(self.subject, 'candidate', return_value=False):
                self.assertEqual(1, self.subject.run_batch())
            clients = [r for r in self.p.state()['modes'] if r['stage'] != 'judge']
            self.assertEqual(21, len(clients))
            self.assertTrue(all(r['outcome'] == 'not_attempted' and r['reason'] == reason for r in clients))
            self.assertEqual(1, collector.call_count)

    def run_admission_fixture(self, skipped_helper=False, missing_capture=False):
        def run_stage(item, configuration, stage, role, seconds, deadline, skip):
            record = self.record(stage, item, configuration, role)
            state = self.p.state(); state['modes'].append(record); self.p.save_state(state)
            if stage != 'judge':
                log, reg, cap = self.evidence(record)
                document = json.loads(cap)
                for slot in document['repeats']:
                    slot['admission'] = 'required'
                    slot['client_id'] = record['job'] + '-pod-r' + str(slot['repeat'])
                if (item, configuration, stage) == ('B08', 'C0', 'read'):
                    if skipped_helper:
                        document['repeats'][0]['admission'] = 'not_applicable_no_delivery'
                        document['repeats'][0]['client_id'] = None
                    if missing_capture:
                        log.write_bytes(v2.REGISTRATION_MARKER + reg + b'\n')
                        return record
                log.write_bytes(v2.REGISTRATION_MARKER + reg + b'\n' + v2.CAPTURE_MARKER + json.dumps(document).encode() + b'\n')
            return record
        with patch.object(self.subject, 'scope', return_value=True), patch.object(self.p, 'admission_allowed', return_value=True), \
             patch.object(self.subject, 'run_stage', side_effect=run_stage), patch.object(self.p, 'run', side_effect=self.fake), \
             patch.object(self.subject, 'candidate', return_value=True):
            self.assertEqual(1 if missing_capture else 0, self.subject.run_batch())
        return json.loads((self.artifacts / 'probe-v2-smoke.json').read_text())

    def test_all_required_transported_slots_report_165_admissions(self):
        report = self.run_admission_fixture()
        self.assertEqual(165, report['admitted_client_slots'])
        self.assertEqual(165, report['maximum_clients'])
        actors = [r for r in self.p.state()['modes'] if r['stage'] != 'judge']
        self.assertEqual(33, len(actors))
        self.assertTrue(all(r['admitted_client_slots'] == 5 and r['capture_complete'] for r in actors))

    def test_skipped_helper_reduces_admissions_without_changing_maximum(self):
        report = self.run_admission_fixture(skipped_helper=True)
        self.assertEqual(164, report['admitted_client_slots'])
        self.assertEqual(165, report['maximum_clients'])
        helper = next(r for r in self.p.state()['modes'] if r['job'] == v2.job_name(RUN, 'B08', 'C0', 'read'))
        self.assertEqual(4, helper['admitted_client_slots'])

    def test_missing_capture_leaves_admitted_slot_total_null(self):
        report = self.run_admission_fixture(missing_capture=True)
        self.assertIsNone(report['admitted_client_slots'])
        self.assertEqual(165, report['maximum_clients'])
        self.assertFalse(report['evidence_passed'])

    def test_admission_exhaustion_records_every_stage_without_execution(self):
        with patch.object(self.subject, 'scope', return_value=True), patch.object(self.p, 'admission_allowed', return_value=False), patch.object(self.p, 'run', side_effect=AssertionError('no command')):
            self.assertEqual(1, self.subject.run_batch())
        records = self.p.state()['modes']
        self.assertEqual(49, len(records))
        self.assertTrue(all(r['outcome'] == 'not_attempted' and r['reason'] == 'start_cap' for r in records))
        self.assertEqual(16, len(self.p.state()['candidates']))


if __name__ == '__main__':
    unittest.main()

"""Synthetic transport regressions; producer integration has its own required fixture command."""
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

    def fake(self, args, seconds, output=None, maximum=4194304):
        self.calls.append((args, seconds, output, maximum))
        if output is not None:
            output.write_text('{}')
        return 0

    def test_selection_is_smoke_all_only_and_legacy_seed_is_noop(self):
        for kind, items, valid in [('smoke', 'all', True), ('batch', 'all', False), ('smoke', 'B08', False), ('smoke', '', False), ('unknown', 'all', False)]:
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
                    self.assertEqual(runtime['eventhubs']['namespace_name'], env[prefix + 'NamespaceName']['value'])
                    self.assertEqual(accounts[account]['client_id'], env[prefix + 'WorkloadIdentityClientId']['value'])
                    rendered_count += 1
        self.assertEqual(49, rendered_count)

    def test_scope_is_private_bound_and_created_immutably_once(self):
        confirmation = {'schema_version': 1, 'source_commit': SHA, 'namespace_id': NS,
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
        for invalid in (None, {'schema_version': 1, 'source_commit': SHA, 'namespace_id': NS, 'accounts': {'loadgen': {'principal_id': ACCOUNTS['loadgen']['principal_id'], 'no_additional_data_access': False}, 'enrichment-consumer': {'principal_id': ACCOUNTS['enrichment-consumer']['principal_id'], 'no_additional_data_access': True}}}):
            with self.subTest(invalid=invalid):
                if invalid is not None:
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
        manifest = json.loads((self.private / f"probe-v2/{record['job']}-evidence.json").read_text())
        self.assertEqual(registration_bytes, manifest['data']['registration.json'].encode())
        self.assertEqual(capture_bytes, manifest['data']['capture.json'].encode())
        self.assertEqual({'registration.json', 'capture.json', 'pod.json'}, set(manifest['data']))
        self.assertTrue(manifest['immutable'])
        self.assertEqual(1, len(self.calls))

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
        manifest = json.loads((self.private / f"probe-v2/{record['job']}-evidence.json").read_text())
        self.assertFalse(json.loads(manifest['data']['capture.json'])['cleanup_completed'])
        self.assertFalse(record['capture_complete'])

    def candidate_run(self, value=None, mutate_receipt=None, mutate_pod=None):
        record = self.record('judge')
        self.evidence(record)
        if mutate_pod:
            path = self.artifacts / record['job'] / 'pods.json'
            data = json.loads(path.read_text()); mutate_pod(data); self.write(path, data)
        raw = json.dumps(value or candidate()).encode()
        receipt = {'schema_version': 2, 'mode': 'single-action-v2-judge', 'run_id': RUN, 'scenario_id': 'B08', 'configuration_id': 'C0',
                   'source_commit': SHA, 'job_name': record['job'], 'pod_name': record['job'] + '-pod', 'pod_uid': 'synthetic-pod-uid',
                   'candidate_name': f'{RUN}/single-action-v2/B08/C0/candidate.json', 'candidate_sha256': hashlib.sha256(raw).hexdigest(),
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

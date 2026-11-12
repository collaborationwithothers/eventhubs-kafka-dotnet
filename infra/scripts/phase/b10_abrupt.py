"""Bounded diagnostic-only orchestration for the owned-child restart trial."""
from datetime import datetime, timezone
import hashlib
import json
import os
import re
import shutil
import sys
import time
import traceback

AUXILIARY_MAXIMUM = 65536
SITE_FILE = os.path.basename(__file__)
JOURNAL_MAXIMUM = 16777216
FIRST_FILES = ('registration.json', 'journal.jsonl', 'abrupt-manifest.json', 'termination.json')
REPORT_FILES = ('same_instance-pre-acceptance-report.json', 'control-post-drain-report.json',
                'cross_restart-pre-acceptance-report.json')
SUCCESSOR_FILES = ('registration.json', 'ledger.jsonl', 'manifest.json', 'statistics.jsonl',
                   'request-lifecycle.jsonl', 'capabilities.json', 'completion.json')
IDENTITY_FIELDS = set('run_id scenario_id configuration_id phase repeat source_commit image_reference job_name pod_name pod_uid instance_id predecessor_instance_id'.split())
ENVELOPE_FIELDS = set('schema_version dialect purpose synthetic scientific_admissible kind identity data'.split())

DATA_FIELDS = {key: set(value.split()) for key, value in {
    'registration': 'registered_utc',
    'child_started': 'pid start_time_ticks boot_id',
    'prepared': 'attempt_id arm role ordinal probe_id key sequence payload_length payload_sha256 predecessor_attempt_id',
    'warmup_prepared': 'attempt_id arm key payload_length payload_sha256',
    'warmup_terminal': 'attempt_id completion_source delivered error_code persistence_status partition offset',
    'pre_acceptance_report': 'arm attempt_id name length sha256 report_identity report_sequence report_capture_utc warmup_attempt_id partition',
    'post_drain_report': 'arm attempt_id name length sha256 report_identity report_sequence report_capture_utc',
    'accepted': 'attempt_id prepared_sequence',
    'synchronous_rejection': 'attempt_id prepared_sequence error_code error_reason',
    'terminal': 'attempt_id completion_source delivered error_code persistence_status',
    'native_error': 'capture_sequence error_code error_name is_error is_fatal callback_monotonic_ticks',
    'cut': 'arm cut_attempt_id release_utc unconfirmed_attempt_ids',
    'control_drained': 'flush_returned out_queue_length',
    'kill_intent': 'arm cut_attempt_id release_utc',
    'signal_failed': 'return_value errno',
    'no_kill': 'reason cross_restart_admitted',
    'gap': 'reason first_missing_sequence capture_status',
    'abrupt_manifest': 'journal journal_records report_files gaps',
    'termination': 'termination outcome detail signal_observation successor_permitted child exit_observed exit_code_local_observation runtime_version wait_bound_milliseconds cgroup_before cgroup_after control_unconfirmed_attempt_ids kill_unconfirmed_attempt_ids capture_gaps native_error_count native_fatal_count native_tail manifest',
    'successor_receipt': 'first_termination journal kill_unconfirmed_attempt_ids registration artifacts ready_utc release_utc invocations cleanup_completed cross_host_timing',
}.items()}
PRODUCING_KINDS = frozenset(('child_started', 'warmup_prepared', 'prepared', 'pre_acceptance_report', 'accepted',
    'synchronous_rejection', 'cut', 'control_drained', 'post_drain_report', 'kill_intent', 'signal_failed', 'no_kill'))

# Emitted outcomes are the closed list in the approved diagnostic contract.
FAILURE_NAMES = frozenset('''
b10_abrupt_diagnostic_not_scientific
b10_abrupt_configuration_invalid
b10_abrupt_platform_unsupported
b10_abrupt_signal_preflight_failed
b10_abrupt_child_start_failed
b10_abrupt_child_identity_mismatch
b10_abrupt_signal_failed
b10_abrupt_exit_unobserved
b10_abrupt_unexpected_exit
b10_abrupt_capture_gap
b10_abrupt_sample_missing
b10_abrupt_warmup_failed
b10_abrupt_leader_not_up
b10_abrupt_report_binding_invalid
b10_abrupt_guard_failed
b10_abrupt_control_incomplete
b10_abrupt_exposure_inconclusive
b10_abrupt_cut_rejected
kill_too_early
kill_too_late
b10_abrupt_cgroup_unavailable
b10_abrupt_oom_conflict
b10_abrupt_publication_failed
b10_abrupt_publication_ambiguous
b10_abrupt_first_termination_unknown
b10_abrupt_successor_binding_invalid
b10_abrupt_successor_late
b10_abrupt_deadline_exhausted
b10_abrupt_timeout_job_unknown
'''.split())


class RecordedFailure(ValueError):
    """A failure already retained in phase-status.json by an inner boundary."""


def execution_modes(configuration):
    if configuration == 'C0':
        return ('loadgen',)
    if configuration == 'C1':
        return ('loadgen-restart', 'loadgen-successor')
    raise ValueError('b10_abrupt_configuration_invalid')


def decode(raw, maximum=AUXILIARY_MAXIMUM):
    if not 0 < len(raw) <= maximum:
        raise ValueError('diagnostic byte bound')
    # As in Phase.single_action_json, inspect nesting before the JSON decoder
    # allocates containers; additionally charge each container's entries here.
    text = raw.decode('utf-8') if isinstance(raw, bytes) else raw
    stack = []
    quoted = escaped = False
    for character in text:
        if quoted:
            if escaped:
                escaped = False
            elif character == "\\":
                escaped = True
            elif character == '"':
                quoted = False
            continue
        if character.isspace():
            continue
        if character in ']}' :
            if not stack or stack[-1][0] != ('[' if character == ']' else '{'):
                raise ValueError('diagnostic nesting')
            stack.pop()
            continue
        if stack:
            if character == ',':
                stack[-1][1] += 1
            elif stack[-1][1] == 0:
                stack[-1][1] = 1
            if stack[-1][1] > 4096:
                raise ValueError('diagnostic entry bound')
        if character == '"':
            quoted = True
        elif character in '[{':
            stack.append([character, 0])
            if len(stack) > 32:
                raise ValueError('diagnostic depth bound')
    if stack or quoted:
        raise ValueError('diagnostic nesting')
    def pairs(items):
        value = {}
        for key, item in items:
            if key in value:
                raise ValueError('duplicate member')
            value[key] = item
        return value
    def invalid(_):
        raise ValueError('nonfinite number')
    value = json.loads(raw, object_pairs_hook=pairs, parse_constant=invalid)
    def depth(item, level=0):
        if level > 32:
            raise ValueError('diagnostic depth bound')
        if isinstance(item, dict):
            for child in item.values():
                depth(child, level + 1)
        elif isinstance(item, list):
            if len(item) > 4096:
                raise ValueError('diagnostic entry bound')
            for child in item:
                depth(child, level + 1)
    depth(value)
    return value


def encode(value):
    return json.dumps(value, separators=(',', ':'), ensure_ascii=True).encode()


def descriptor(name, raw):
    return {'name': name, 'length': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}


def bound(reference, name, raw):
    if (not isinstance(reference, dict) or set(reference) != {'name', 'length', 'sha256'}
            or type(reference['length']) is not int or reference != descriptor(name, raw)):
        raise ValueError('diagnostic descriptor mismatch')


def nonnegative(value):
    return type(value) is int and 0 <= value <= 9223372036854775807


def leader_up(raw, topic, partition):
    """Check the selected partition's unique leader in the published native report."""
    try:
        report = decode(raw, 1048576)
        leader = report['topics'][topic]['partitions'][str(partition)]['leader']
        brokers = report['brokers']
        if type(leader) is not int or leader < 0 or not isinstance(brokers, dict):
            return False
        matching = [broker for broker in brokers.values() if isinstance(broker, dict)
                    and type(broker.get('nodeid')) is int and broker['nodeid'] == leader]
        return len(matching) == 1 and matching[0].get('state') == 'UP'
    except (ValueError, LookupError, TypeError, AttributeError):
        return False


def required_string(value):
    return isinstance(value, str) and bool(value)


def string_array(value):
    return (isinstance(value, list) and all(required_string(item) for item in value)
            and len(value) == len(set(value)))


def reference(value):
    return (isinstance(value, dict) and set(value) == {'name', 'length', 'sha256'}
            and required_string(value['name']) and '/' not in value['name'] and '\\' not in value['name']
            and '..' not in value['name'] and nonnegative(value['length'])
            and isinstance(value['sha256'], str) and re.fullmatch('[0-9a-f]{64}', value['sha256']) is not None)


def failure_site(error, caller):
    """Name the raising line by file base name, function and line number; never by message, value or path."""
    frames = traceback.extract_tb(error.__traceback__) if error.__traceback__ is not None else []
    local = [frame for frame in frames if os.path.basename(frame.filename) == SITE_FILE]
    origin = frames[-1] if frames else None
    return {'file': SITE_FILE if local else os.path.basename(caller.f_code.co_filename),
            'function': local[-1].name if local else caller.f_code.co_name,
            'line': local[-1].lineno if local else caller.f_lineno,
            'origin_file': os.path.basename(origin.filename) if origin else None,
            'origin_function': origin.name if origin else None,
            'origin_line': origin.lineno if origin else None}


class Trial:
    def __init__(self, phase, failure=ValueError):
        self.p = phase
        self.failure = failure

    @property
    def directory(self):
        return self.p.artifacts / 'b10-abrupt-diagnostic'

    def record_failure(self, error, fallback, boundary, safety=False, cause=None, caller=None):
        if isinstance(error, RecordedFailure):
            return
        message = str(error)
        binding_errors = {'diagnostic identity', 'selection checkpoint', 'selection stale', 'selection identity',
                          'prepared selection binding', 'selection payload', 'release boundary', 'successor release'}
        reason = (message if message in FAILURE_NAMES else
                  'b10_abrupt_successor_binding_invalid' if message in binding_errors else
                  'b10_abrupt_publication_failed' if message == 'diagnostic descriptor mismatch' else fallback)
        if reason not in FAILURE_NAMES:
            raise ValueError('unsupported diagnostic failure mapping')
        state = self.p.state()
        state.setdefault('b10_abrupt_gaps', []).append(reason)
        state.setdefault('b10_abrupt_diagnostics', []).append({
            'reason': reason, 'boundary': boundary, 'exception_type': type(error).__name__,
            'validation_detail': message, 'site': failure_site(cause or error, caller or sys._getframe(1)),
            'cause_type': type(cause).__name__ if cause is not None else None})
        state['candidate'] = None
        state['safety_stop'] = (state.get('safety_stop', False) or safety
                                or reason == 'b10_abrupt_first_termination_unknown')
        self.p.save_state(state)

    def fail(self, reason, safety=False, cause=None):
        self.record_failure(ValueError(reason), reason, 'admission', safety, cause=cause, caller=sys._getframe(1))
        raise RecordedFailure(reason)

    def prepare(self):
        expected = {'PARCHWELL_SCENARIO': 'B10', 'PARCHWELL_PHASE': 'C1',
                    'PARCHWELL_CONFIGURATION': 'C1', 'PARCHWELL_REPEAT': '1',
                    'PARCHWELL_RUN_KIND': 'smoke', 'PARCHWELL_PURPOSE': 'diagnostic_trial',
                    'PARCHWELL_B10_ABRUPT_DIAGNOSTIC': 'true', 'PARCHWELL_CONFIRM_DIAGNOSTIC_TRIAL': 'true'}
        if (any(os.environ.get(key) != value for key, value in expected.items())
                or any(key.lower().startswith('b10abrupt__') for key in os.environ)):
            raise self.failure('b10_abrupt_configuration_invalid')
        return self.p.prepare_shared('B10', 'C1', 'C1', 'b10d', purpose='diagnostic_trial',
                                     run_kind='smoke', b10_abrupt_diagnostic=True)

    def phase(self):
        phase = self.p.read('phase.json')
        if any(phase.get(key) != value for key, value in {'scenario': 'B10', 'configuration': 'C1',
                'phase': 'C1', 'purpose': 'diagnostic_trial', 'run_kind': 'smoke',
                'b10_abrupt_diagnostic': True}.items()):
            raise self.failure('b10_abrupt_configuration_invalid')
        return phase

    def values(self, mode, values=None):
        phase = self.phase()
        if mode not in execution_modes('C1'):
            raise ValueError('b10_abrupt_configuration_invalid')
        values = values or self.p.values(mode, 1)
        role = 'first' if mode == 'loadgen-restart' else 'successor'
        values['runtime'].update(b10_abrupt_diagnostic=True, run_kind='smoke',
            b10_abrupt_binding_configmap=phase['run_id'] + '-b10-abrupt-' + role)
        return values

    def configmap(self, role, binding):
        phase = self.phase()
        raw = encode(binding)
        if len(raw) > AUXILIARY_MAXIMUM:
            raise ValueError('b10_abrupt_publication_failed')
        if role == 'first':
            (self.p.private / 'b10-abrupt-start-line-binding.json').write_bytes(raw)
        value = {'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {
            'name': phase['run_id'] + '-b10-abrupt-' + role, 'namespace': 'parchwell',
            'labels': {'app.kubernetes.io/instance': phase['run_id']}},
            'immutable': True, 'data': {'binding.json': raw.decode()}}
        path = self.p.private / ('b10-abrupt-' + role + '-configmap.json')
        path.write_bytes(encode(value))
        path.chmod(0o600)
        if self.p.run(['kubectl', 'create', '-f', str(path), '--request-timeout=30s'], 60):
            raise ValueError('b10_abrupt_publication_failed')
        return value

    def metadata(self, record, deadline):
        phase = self.phase()
        prefix = phase['run_id'] + '/C1/r1/'
        files = {}
        for name in ('start-line.json', 'broker-inventory.json', 'start-line-completion.json'):
            raw = self.p.download_blob(prefix + name, self.directory / name, AUXILIARY_MAXIMUM, deadline)
            if raw is None:
                raise ValueError('b10_abrupt_publication_ambiguous')
            files[name] = raw
        receipt = decode(files['start-line-completion.json'])
        for key, expected in {'schema_version': 1, 'mode': 'start-line', 'scenario_id': 'B10',
                'configuration_id': 'C1', 'run_id': phase['run_id'], 'phase': 'C1', 'repeat': 1,
                'source_commit': phase['source_commit'], 'job_name': record['job'],
                'handler_exit_code': 0, 'cleanup_completed': True}.items():
            if receipt.get(key) != expected:
                raise ValueError('start-line receipt identity')
        if len(receipt['artifacts']) != 2:
            raise ValueError('start-line receipt inventory')
        for name, reference in zip(('start-line.json', 'broker-inventory.json'), receipt['artifacts']):
            if reference.get('container') != 'verifier':
                raise ValueError('start-line container')
            bound({key: reference[key] for key in ('name', 'length', 'sha256')}, prefix + name, files[name])
            document = decode(files[name])
            identity = document['identity']
            if any(identity.get(key) != value for key, value in {'scenario_id': 'B10',
                    'configuration_id': 'C1', 'run_id': phase['run_id']}.items()):
                raise ValueError('start-line identity')
            first_job = self.p.mode_job(phase, 'loadgen', 1)
            if any(identity.get(key) != first_job for key in ('instance_id', 'expected_instance_id', 'client_id')):
                raise ValueError('start-line producer identity')
            if name == 'start-line.json':
                if (document.get('verifier_commit') != phase['source_commit'] or document.get('expected_job_names') !=
                        [first_job, self.p.mode_job(phase, 'loadgen2', 1)]):
                    raise ValueError('start-line source or jobs')
            elif document.get('source_commit') != phase['source_commit'] or document.get('source') != 'kafka_metadata':
                raise ValueError('broker inventory source')
        return self.configmap('first', {'start_line': files['start-line.json'].decode(),
            'broker_inventory': files['broker-inventory.json'].decode(),
            'completion': files['start-line-completion.json'].decode()})

    def job(self, mode, deadline):
        phase = self.phase()
        name = self.p.mode_job(phase, mode, 1)
        record = {'job': name, 'mode': mode, 'repeat': 1, 'outcome': 'not_attempted',
                  **{key: None for key in ('render', 'apply', 'wait', 'collect', 'delete', 'confirm')}}
        state = self.p.state()
        state['modes'].append(record)
        self.p.save_state(state)
        budget = self.p.render(mode, 1)
        record['render'] = self.p.render_code
        if budget is None:
            record['outcome'] = 'render_failed'
        else:
            path = self.p.private / ('rendered-' + name + '.yaml')
            record['apply'] = self.p.run(['kubectl', 'apply', '-f', str(path), '--request-timeout=30s'], 60)
            if record['apply']:
                record['outcome'] = 'apply_failed'
            else:
                record['wait'] = self.p.run(['bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job', name, str(budget)], budget + 90)
                record['outcome'] = 'complete' if record['wait'] == 0 else 'failed' if record['wait'] == 1 else 'unknown'
        state = self.p.state()
        state['modes'][-1] = record
        if record['outcome'] in ('unknown', 'apply_failed'):
            state['safety_stop'] = True
        self.p.save_state(state)
        return record

    def _observe_timeout_termination(self, record):
        """Keep an uncertain ordinary producer from overlapping the restart producer."""
        try:
            path = self.directory / 'loadgen-pods.json'
            code = self.p.run(['kubectl', 'get', 'pods', '-n', 'parchwell', '-l',
                'job-name=' + record['job'], '-o', 'json', '--request-timeout=30s'], 30, output=path)
            if code:
                raise ValueError('timeout pod observation failed')
            pods = decode(path.read_bytes(), 4 * 1024 * 1024)['items']
            if len(pods) != 1:
                raise ValueError('timeout pod count')
            pod = pods[0]
            owners = [owner for owner in pod['metadata']['ownerReferences'] if owner.get('controller') is True]
            if len(owners) != 1 or owners[0].get('kind') != 'Job' or owners[0].get('name') != record['job']:
                raise ValueError('timeout pod owner')
            containers = pod['spec']['containers']
            statuses = pod['status']['containerStatuses']
            if (len(containers) != 1 or len(statuses) != 1
                    or containers[0]['name'] != 'loadgen' or statuses[0]['name'] != 'loadgen'
                    or containers[0]['image'] != self.phase()['images']['loadgen']['reference']):
                raise ValueError('timeout pod image')
            if type(statuses[0]['state']['terminated']['exitCode']) is not int:
                raise ValueError('timeout pod termination')
        except Exception:
            record['outcome'] = 'unknown'
            state = self.p.state()
            state['modes'][-1] = record
            self.p.save_state(state)

    def observe_first_termination(self, record):
        """Query pod state directly, before log collection can spend the release window."""
        phase = self.phase()
        path = self.directory / (record['mode'] + '-pods.json')
        code = self.p.run(['kubectl', 'get', 'pods', '-n', 'parchwell', '-l',
            'job-name=' + record['job'], '-o', 'json', '--request-timeout=30s'], 30, output=path)
        try:
            if code:
                raise ValueError('pod observation failed')
            pods = decode(path.read_bytes(), 4 * 1024 * 1024)['items']
            if len(pods) != 1:
                raise ValueError('pod count')
            pod = pods[0]
            metadata, spec = pod['metadata'], pod['spec']
            owners = [owner for owner in metadata['ownerReferences'] if owner.get('controller') is True]
            if len(owners) != 1 or owners[0].get('kind') != 'Job' or owners[0].get('name') != record['job']:
                raise ValueError('pod owner')
            containers = [entry for entry in spec['containers'] if entry['name'] == 'loadgen']
            statuses = [entry for entry in pod['status']['containerStatuses'] if entry['name'] == 'loadgen']
            image = phase['images']['loadgen']['reference']
            if len(containers) != 1 or len(statuses) != 1 or containers[0]['image'] != image:
                raise ValueError('pod image')
            environment = containers[0]['env']
            for key, expected in [('B10Abrupt__SourceCommit', phase['source_commit']), ('B10Abrupt__JobName', record['job'])]:
                entries = [entry for entry in environment if entry['name'] == key]
                if len(entries) != 1 or entries[0].get('value') != expected:
                    raise ValueError('pod source')
            termination = statuses[0]['state']['terminated']
            if type(termination['exitCode']) is not int:
                raise ValueError('pod termination')
            finished, _ = self.p.b10_utc(termination['finishedAt'])
            if not re.fullmatch(r'[a-z0-9][a-z0-9-]*', metadata['name']) or not metadata['uid']:
                raise ValueError('pod identity')
            observation = {'job_name': record['job'], 'pod_name': metadata['name'], 'pod_uid': metadata['uid'],
                'image_reference': image, 'source_commit': phase['source_commit'], 'finished_utc': finished}
            return observation
        except (ValueError, OSError, LookupError, TypeError, AttributeError) as error:
            self.fail('b10_abrupt_first_termination_unknown', safety=True, cause=error)

    def envelope(self, raw, kind, observation, predecessor=None, maximum=AUXILIARY_MAXIMUM):
        value = decode(raw, maximum)
        if (not isinstance(value, dict) or set(value) != ENVELOPE_FIELDS or type(value['schema_version']) is not int or value['schema_version'] != 3
                or value['dialect'] != 'b10_abrupt' or value['purpose'] != 'diagnostic_trial'
                or value['synthetic'] is not True or value['scientific_admissible'] is not False or value['kind'] != kind):
            raise ValueError('diagnostic envelope')
        identity = value['identity']
        phase = self.phase()
        expected = {'run_id': phase['run_id'], 'scenario_id': 'B10', 'configuration_id': 'C1', 'phase': 'C1',
                    'repeat': 1, 'source_commit': phase['source_commit'], 'image_reference': phase['images']['loadgen']['reference'],
                    'job_name': observation['job_name'], 'pod_name': observation['pod_name'], 'pod_uid': observation['pod_uid'],
                    'instance_id': observation['pod_name'], 'predecessor_instance_id': predecessor}
        if not isinstance(identity, dict) or set(identity) != IDENTITY_FIELDS or identity != expected or type(identity['repeat']) is not int:
            raise ValueError('diagnostic identity')
        if kind not in DATA_FIELDS or not isinstance(value['data'], dict) or set(value['data']) != DATA_FIELDS[kind]:
            if kind in ('warmup_prepared', 'warmup_terminal'):
                raise ValueError('journal malformed_frame')
            raise ValueError('diagnostic data members')
        self.validate_data(kind, value['data'])
        return value

    def validate_data(self, kind, data):
        if kind == 'warmup_prepared':
            if (data['arm'] not in ('same_instance', 'cross_restart')
                    or not all(required_string(data[key]) for key in ('attempt_id', 'key'))
                    or not nonnegative(data['payload_length']) or data['payload_length'] > 900000
                    or not isinstance(data['payload_sha256'], str)
                    or not re.fullmatch('[0-9a-f]{64}', data['payload_sha256'])):
                raise ValueError('journal malformed_frame')
        if kind == 'warmup_terminal':
            if (not required_string(data['attempt_id']) or data['completion_source'] != 'callback'
                    or type(data['delivered']) is not bool
                    or any(type(data[key]) is not int or not -2147483648 <= data[key] <= 2147483647
                           for key in ('error_code', 'partition'))
                    or data['persistence_status'] is not None and (type(data['persistence_status']) is not int
                        or not -2147483648 <= data['persistence_status'] <= 2147483647)
                    or type(data['offset']) is not int or not -9223372036854775808 <= data['offset'] <= 9223372036854775807
                    or data['delivered'] and (data['partition'] < 0 or data['offset'] < 0)):
                raise ValueError('journal malformed_frame')
        integer_fields = {
            'child_started': ('pid', 'start_time_ticks'),
            'prepared': ('ordinal', 'sequence', 'payload_length'),
            'accepted': ('prepared_sequence',), 'synchronous_rejection': ('prepared_sequence',),
            'native_error': ('capture_sequence', 'callback_monotonic_ticks'),
            'control_drained': ('out_queue_length',),
            'abrupt_manifest': ('journal_records',),
            'termination': ('wait_bound_milliseconds', 'native_error_count', 'native_fatal_count'),
            'pre_acceptance_report': ('length', 'report_sequence'),
            'post_drain_report': ('length', 'report_sequence'),
        }
        string_fields = {
            'child_started': ('boot_id',),
            'prepared': ('attempt_id', 'probe_id', 'key'),
            'accepted': ('attempt_id',), 'synchronous_rejection': ('attempt_id', 'error_reason'),
            'terminal': ('attempt_id', 'completion_source'), 'native_error': ('error_name',),
            'cut': ('cut_attempt_id',), 'kill_intent': ('cut_attempt_id',),
            'termination': ('runtime_version',),
        }
        bool_fields = {'terminal': ('delivered',), 'native_error': ('is_error', 'is_fatal'),
            'control_drained': ('flush_returned',), 'termination': ('successor_permitted', 'exit_observed'),
            'successor_receipt': ('cleanup_completed',), 'no_kill': ('cross_restart_admitted',)}
        if (any(not nonnegative(data[key]) for key in integer_fields.get(kind, ()))
                or any(not required_string(data[key]) for key in string_fields.get(kind, ()))
                or any(type(data[key]) is not bool for key in bool_fields.get(kind, ()))):
            raise ValueError('diagnostic data types')
        for key in ('error_code', 'return_value', 'errno'):
            if key in data and (type(data[key]) is not int or not -2147483648 <= data[key] <= 2147483647):
                raise ValueError('diagnostic data types')
        if kind == 'child_started' and data['pid'] == 0:
            raise ValueError('diagnostic child pid')
        if kind == 'terminal' and (data['completion_source'] != 'callback' or
                data['persistence_status'] is not None and (type(data['persistence_status']) is not int
                    or not -2147483648 <= data['persistence_status'] <= 2147483647)):
            raise ValueError('terminal binding')
        if kind in ('cut', 'kill_intent', 'prepared', 'pre_acceptance_report', 'post_drain_report'):
            if data['arm'] not in ('same_instance', 'cross_restart'):
                raise ValueError('diagnostic arm')
        if kind == 'prepared':
            original = data['role'] == 'original'
            if (data['role'] not in ('original', 'resend') or data['ordinal'] > 4
                    or data['sequence'] != data['ordinal'] or data['payload_length'] > 900000
                    or original and data['predecessor_attempt_id'] is not None
                    or not original and not required_string(data['predecessor_attempt_id'])
                    or not isinstance(data['payload_sha256'], str)
                    or not re.fullmatch('[0-9a-f]{64}', data['payload_sha256'])):
                raise ValueError('prepared binding')
        if kind in ('cut', 'kill_intent'):
            self.utc_ticks(data['release_utc'])
        if kind == 'cut' and not string_array(data['unconfirmed_attempt_ids']):
            raise ValueError('selection stale')
        if kind in ('pre_acceptance_report', 'post_drain_report'):
            if (not reference({key: data[key] for key in ('name', 'length', 'sha256')})
                    or data['length'] == 0 or data['length'] > 1048576 or data['report_sequence'] == 0
                    or kind == 'pre_acceptance_report' and not required_string(data['attempt_id'])
                    or kind == 'post_drain_report' and (data['arm'] != 'same_instance' or data['attempt_id'] is not None)):
                raise ValueError('report binding')
        if kind == 'pre_acceptance_report' and (not required_string(data['warmup_attempt_id'])
                or type(data['partition']) is not int or not -2147483648 <= data['partition'] <= 2147483647):
            raise ValueError('b10_abrupt_report_binding_invalid')
        if kind in ('registration', 'pre_acceptance_report', 'post_drain_report'):
            self.utc_ticks(data['registered_utc'] if kind == 'registration' else data['report_capture_utc'])
        if kind in ('abrupt_manifest', 'termination', 'successor_receipt'):
            for key in ('journal', 'manifest', 'first_termination', 'registration'):
                if key in data and not reference(data[key]):
                    raise ValueError('diagnostic descriptor mismatch')
            for key in ('gaps', 'capture_gaps', 'control_unconfirmed_attempt_ids', 'kill_unconfirmed_attempt_ids'):
                if key in data and not string_array(data[key]):
                    raise ValueError('diagnostic data types')
            for key in ('report_files', 'artifacts'):
                if key in data and (not isinstance(data[key], list) or not all(reference(entry) for entry in data[key])):
                    raise ValueError('diagnostic descriptor mismatch')
        if kind == 'termination':
            if (data['termination'] not in ('intended_self_kill', 'no_kill', 'unexpected_exit', 'unknown')
                    or any(data[key] is not None and not required_string(data[key]) for key in ('outcome', 'detail', 'native_tail'))
                    or data['signal_observation'] is not None and (not isinstance(data['signal_observation'], dict)
                        or set(data['signal_observation']) != DATA_FIELDS['signal_failed'])
                    or data['exit_code_local_observation'] is not None and (type(data['exit_code_local_observation']) is not int
                        or not -2147483648 <= data['exit_code_local_observation'] <= 2147483647)):
                raise ValueError('diagnostic data types')
        if kind == 'successor_receipt':
            self.utc_ticks(data['ready_utc'])
            self.utc_ticks(data['release_utc'])
            if data['cross_host_timing'] != 'not_verified' or not isinstance(data['invocations'], list):
                raise ValueError('b10_abrupt_successor_binding_invalid')
            for invocation in data['invocations']:
                if (not isinstance(invocation, dict) or set(invocation) != {'attempt_id', 'predecessor_attempt_id',
                        'key', 'sequence', 'payload_sha256', 'invocation_utc', 'monotonic_ticks'}
                        or any(not required_string(invocation[key]) for key in ('attempt_id', 'predecessor_attempt_id', 'key'))
                        or not nonnegative(invocation['sequence']) or not nonnegative(invocation['monotonic_ticks'])
                        or not isinstance(invocation['payload_sha256'], str) or not re.fullmatch('[0-9a-f]{64}', invocation['payload_sha256'])):
                    raise ValueError('b10_abrupt_successor_binding_invalid')
                self.utc_ticks(invocation['invocation_utc'])

    def download(self, observation, names, deadline):
        phase = self.phase()
        prefix = phase['run_id'] + '/C1/r1/b10-abrupt-diagnostic/' + observation['pod_name'] + '/'
        target = self.directory / observation['pod_name']
        target.mkdir(exist_ok=True)
        files = {}
        for name in names:
            limits = {'journal.jsonl': JOURNAL_MAXIMUM, 'ledger.jsonl': 268435456, 'manifest.json': 67108864,
                      'statistics.jsonl': 134217728, 'request-lifecycle.jsonl': 134217728, **dict.fromkeys(REPORT_FILES, 1048576)}
            limit = min(limits.get(name, AUXILIARY_MAXIMUM), 1073741824 - sum(map(len, files.values())))
            raw = self.p.download_blob(prefix + name, target / name, limit, deadline, container='ledger', minimum=0 if name == 'request-lifecycle.jsonl' else 1)
            if raw is None:
                raise ValueError('b10_abrupt_publication_ambiguous')
            files[name] = raw
        if sum(map(len, files.values())) > 1073741824:
            raise ValueError('b10_abrupt_publication_failed')
        return files

    def utc_ticks(self, value):
        if not isinstance(value, str) or not re.fullmatch(r'\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}\+00:00', value):
            raise ValueError('journal utc')
        return self.p.b10_utc(value)[1]

    def read_journal(self, raw, observation):
        if not 0 < len(raw) <= JOURNAL_MAXIMUM or not raw.endswith(b'\n'):
            raise ValueError('journal malformed_frame')
        if raw.count(b'\n') > 4096:
            raise ValueError('journal entry bound')
        lines = raw[:-1].split(b'\n')
        if not 0 < len(lines) <= 4096:
            raise ValueError('journal entry bound')
        frames = []
        for sequence, line in enumerate(lines, 1):
            frame = decode(line, 65536)
            if (set(frame) != ENVELOPE_FIELDS | {'sequence', 'observed_utc', 'monotonic_ticks', 'clock_frequency'}
                    or type(frame['sequence']) is not int or frame['sequence'] != sequence):
                raise ValueError('journal sequence_gap')
            if any(type(frame[key]) is not int or not 0 <= frame[key] <= 9223372036854775807
                   for key in ('monotonic_ticks', 'clock_frequency')) or frame['clock_frequency'] == 0:
                raise ValueError('journal clock')
            self.utc_ticks(frame['observed_utc'])
            if frame['kind'] not in PRODUCING_KINDS | {'terminal', 'warmup_terminal', 'native_error', 'gap'}:
                raise ValueError('journal record kind')
            self.envelope(encode({key: frame[key] for key in ENVELOPE_FIELDS}), frame['kind'], observation, maximum=JOURNAL_MAXIMUM)
            if frame['kind'] == 'gap':
                raise ValueError('journal capture gap')
            frames.append(frame)
        if frames[0]['kind'] != 'child_started' or sum(frame['kind'] == 'child_started' for frame in frames) != 1:
            raise ValueError('journal child identity')
        self.validate_warmups(frames)
        return frames

    def validate_warmups(self, frames):
        prepared, terminals = {}, {}
        for frame in frames:
            kind, data = frame['kind'], frame['data']
            if kind == 'warmup_prepared':
                arm = data['arm']
                if (arm in prepared
                        or data['attempt_id'] != frame['identity']['instance_id'] + '-' + arm + '-warmup'
                        or data['key'] != 'B10/' + self.phase()['run_id'] + '/C1/1/' + arm
                        or arm == 'cross_restart' and not self.control_complete_before(frames, frame['sequence'])):
                    raise ValueError('journal malformed_frame')
                prepared[arm] = frame
            elif kind == 'warmup_terminal':
                warmup = next((item for item in prepared.values()
                               if item['data']['attempt_id'] == data['attempt_id']), None)
                if warmup is None or data['attempt_id'] in terminals:
                    raise ValueError('journal malformed_frame')
                terminals[data['attempt_id']] = frame
            elif kind == 'prepared' and data['role'] == 'original' and data['ordinal'] == 0:
                warmup = prepared.get(data['arm'])
                terminal = terminals.get(warmup['data']['attempt_id']) if warmup else None
                if (terminal is None or terminal['data']['delivered'] is not True
                        or terminal['data']['error_code'] != 0 or terminal['data']['persistence_status'] not in (0, 2)):
                    raise ValueError('journal malformed_frame')
            elif kind == 'pre_acceptance_report':
                warmup = prepared.get(data['arm'])
                terminal = terminals.get(warmup['data']['attempt_id']) if warmup else None
                if (terminal is None or data['warmup_attempt_id'] != warmup['data']['attempt_id']
                        or data['partition'] != terminal['data']['partition']
                        or self.utc_ticks(data['report_capture_utc']) <= self.utc_ticks(terminal['observed_utc'])):
                    raise ValueError('b10_abrupt_report_binding_invalid')

    def control_complete_before(self, frames, sequence):
        prior = [frame for frame in frames if frame['sequence'] < sequence]
        attempts = [frame for frame in prior if frame['kind'] == 'prepared' and frame['data']['arm'] == 'same_instance']
        originals = [frame['data']['ordinal'] for frame in attempts if frame['data']['role'] == 'original']
        cuts = [frame for frame in prior if frame['kind'] == 'cut']
        drains = [frame for frame in prior if frame['kind'] == 'control_drained']
        reports = [frame for frame in prior if frame['kind'] == 'post_drain_report']
        if (originals != list(range(5)) or len(cuts) != 1 or not cuts[0]['data']['unconfirmed_attempt_ids']
                or len(drains) != 1 or len(reports) != 1 or drains[0]['data']['flush_returned'] is not True
                or drains[0]['data']['out_queue_length'] != 0
                or not cuts[0]['sequence'] < drains[0]['sequence'] < reports[0]['sequence']):
            return False
        resends = [frame for frame in attempts if frame['data']['role'] == 'resend']
        if ([frame['data']['predecessor_attempt_id'] for frame in resends] != cuts[0]['data']['unconfirmed_attempt_ids']
                or any(not cuts[0]['sequence'] < frame['sequence'] < drains[0]['sequence']
                       or self.utc_ticks(frame['observed_utc']) < self.utc_ticks(cuts[0]['data']['release_utc']) for frame in resends)):
            return False
        accepted = {frame['data']['attempt_id'] for frame in prior if frame['kind'] == 'accepted'}
        terminal = {frame['data']['attempt_id'] for frame in prior if frame['kind'] == 'terminal'}
        rejected = {frame['data']['attempt_id'] for frame in prior if frame['kind'] == 'synchronous_rejection'}
        return all(frame['data']['attempt_id'] in rejected
                   or frame['data']['attempt_id'] in accepted & terminal for frame in attempts)

    def accept_diagnostic_publication(self, observation, files):
        if not set(FIRST_FILES) <= set(files) <= set(FIRST_FILES + REPORT_FILES):
            raise ValueError('b10_abrupt_publication_ambiguous')
        receipt_identity = decode(files['termination.json'])['identity']
        if any(receipt_identity.get(key) != observation.get(key) for key in ('job_name', 'pod_name', 'pod_uid')):
            self.fail('b10_abrupt_first_termination_unknown', safety=True)
        if observation['job_name'] != self.phase()['run_id'] + '-c1-r1-loadgen-restart':
            raise ValueError('diagnostic identity')
        objects = {name: self.envelope(files[name], kind, observation) for name, kind in
                   (('registration.json', 'registration'), ('abrupt-manifest.json', 'abrupt_manifest'),
                    ('termination.json', 'termination'))}
        termination, manifest = (objects[name]['data'] for name in ('termination.json', 'abrupt-manifest.json'))
        bound(termination['manifest'], 'abrupt-manifest.json', files['abrupt-manifest.json'])
        bound(manifest['journal'], 'journal.jsonl', files['journal.jsonl'])
        frames = self.read_journal(files['journal.jsonl'], observation)
        if (type(manifest['journal_records']) is not int or manifest['journal_records'] != len(frames)
                or manifest['gaps'] != [] or termination['capture_gaps'] != []
                or termination['successor_permitted'] is not True or termination['termination'] != 'intended_self_kill'
                or termination['exit_observed'] is not True or type(termination['exit_code_local_observation']) is not int
                or termination['exit_code_local_observation'] != 137
                or termination['native_tail'] != 'unknown_due_to_intended_kill'
                or any(termination[key] is not None for key in ('outcome', 'detail', 'signal_observation'))):
            raise ValueError('b10_abrupt_publication_failed')
        child = termination['child']
        if (not isinstance(child, dict) or set(child) != {'pid', 'start_time_ticks', 'boot_id', 'parent_observed_start_time_ticks'}
                or any(child.get(key) != value for key, value in frames[0]['data'].items())
                or not nonnegative(child['parent_observed_start_time_ticks'])
                or child['start_time_ticks'] != child['parent_observed_start_time_ticks']):
            raise ValueError('b10_abrupt_child_identity_mismatch')
        before, after = termination['cgroup_before'], termination['cgroup_after']
        cgroup_fields = {'boot_id', 'cgroup_path', 'directory_identity', 'oom_kill'}
        if (not isinstance(before, dict) or not isinstance(after, dict) or set(before) != cgroup_fields
                or set(after) != cgroup_fields or before != after or type(before['oom_kill']) is not int
                or not 0 <= before['oom_kill'] <= 9223372036854775807):
            raise ValueError('b10_abrupt_oom_conflict')
        if (before['boot_id'] != child['boot_id']
                or any(not isinstance(before[key], str) or not before[key] for key in ('boot_id', 'cgroup_path', 'directory_identity'))):
            raise ValueError('b10_abrupt_cgroup_unavailable')
        prepared, accepted, terminals, rejections = {}, [], set(), set()
        control, kill, reports, cut, intent = None, None, [], None, None
        last_producing = None
        for frame in frames:
            kind, data = frame['kind'], frame['data']
            if kind == 'prepared':
                attempt = data['attempt_id']
                if (not isinstance(attempt, str) or not attempt or attempt in prepared
                        or data['arm'] not in ('same_instance', 'cross_restart') or data['role'] not in ('original', 'resend')
                        or type(data['ordinal']) is not int or not 0 <= data['ordinal'] <= 4
                        or type(data['sequence']) is not int or data['sequence'] != data['ordinal']
                        or data['key'] != 'B10/' + self.phase()['run_id'] + '/C1/1/' + data['arm']
                        or data['probe_id'] != data['key']
                        or type(data['payload_length']) is not int or not 0 <= data['payload_length'] <= 900000
                        or not isinstance(data['payload_sha256'], str) or not re.fullmatch('[0-9a-f]{64}', data['payload_sha256'])):
                    raise ValueError('prepared binding')
                if data['arm'] == 'cross_restart':
                    control_prepared = [item['data']['attempt_id'] for item in prepared.values() if item['data']['arm'] == 'same_instance']
                    if ('control-post-drain-report.json' not in [entry['name'] for entry in reports]
                            or any(item not in terminals and item not in rejections for item in control_prepared)):
                        raise ValueError('b10_abrupt_control_incomplete')
                prepared[attempt] = frame
            elif kind in ('accepted', 'synchronous_rejection'):
                attempt = data['attempt_id']
                original = prepared.get(attempt)
                if (original is None or original['sequence'] != data['prepared_sequence']
                        or type(data['prepared_sequence']) is not int or attempt in accepted or attempt in rejections):
                    raise ValueError('prepared acceptance binding')
                if kind == 'accepted':
                    if original['data']['role'] == 'original' and original['data']['ordinal'] == 0:
                        if original['data']['arm'] + '-pre-acceptance-report.json' not in [entry['name'] for entry in reports]:
                            raise ValueError('report binding')
                    accepted.append(attempt)
                else:
                    rejections.add(attempt)
            elif kind == 'terminal':
                attempt = data['attempt_id']
                if attempt not in prepared or attempt in terminals or attempt in rejections or type(data['delivered']) is not bool:
                    raise ValueError('terminal binding')
                if (data['delivered'] != (data['error_code'] == 0)
                        or (data['error_code'], data['persistence_status']) not in ((0, 0), (0, 2), (-192, 0), (-192, 1))):
                    raise ValueError('b10_abrupt_guard_failed')
                terminals.add(attempt)
            elif kind in ('cut', 'kill_intent'):
                arm = 'same_instance' if kind == 'cut' else 'cross_restart'
                original = prepared.get(data['cut_attempt_id'])
                if (data['arm'] != arm or original is None or original['data']['arm'] != arm
                        or original['data']['role'] != 'original' or original['data']['ordinal'] != 4
                        or data['cut_attempt_id'] not in accepted
                        or last_producing is None or last_producing['kind'] != 'accepted'
                        or last_producing['data']['attempt_id'] != data['cut_attempt_id']
                        or self.utc_ticks(data['release_utc']) - self.utc_ticks(frame['observed_utc']) != 120 * 10000000):
                    raise ValueError('release boundary')
                unconfirmed = [attempt for attempt in accepted if prepared[attempt]['data']['arm'] == arm
                    and prepared[attempt]['data']['role'] == 'original' and attempt not in terminals and attempt not in rejections]
                if kind == 'cut':
                    if cut is not None or data['unconfirmed_attempt_ids'] != unconfirmed:
                        raise ValueError('selection stale')
                    control, cut = unconfirmed, frame
                else:
                    if intent is not None:
                        raise ValueError('selection stale')
                    intent = frame
            elif kind in ('pre_acceptance_report', 'post_drain_report'):
                name = data['name']
                expected_name = (data['arm'] + '-pre-acceptance-report.json' if kind == 'pre_acceptance_report'
                                 else 'control-post-drain-report.json')
                identity = data['report_identity']
                if (name != expected_name or name not in REPORT_FILES or name in [entry['name'] for entry in reports]
                        or not isinstance(identity, dict) or set(identity) != {'scenario_id', 'run_id', 'role', 'client_id'}
                        or identity['scenario_id'] != 'B10' or identity['run_id'] != self.phase()['run_id']
                        or identity['role'] != 'LoadGenProducer' or not isinstance(identity['client_id'], str) or not identity['client_id']
                        or type(data['report_sequence']) is not int or not 1 <= data['report_sequence'] <= 9223372036854775807
                        or type(data['length']) is not int or not 1 <= data['length'] <= 1048576):
                    raise ValueError('report binding')
                captured = self.utc_ticks(data['report_capture_utc'])
                if kind == 'pre_acceptance_report':
                    original = prepared.get(data['attempt_id'])
                    if (original is None or original['data']['arm'] != data['arm']
                            or original['data']['role'] != 'original' or original['data']['ordinal'] != 0
                            or data['attempt_id'] in accepted):
                        raise ValueError('report binding')
                else:
                    prior_drains = [item for item in frames if item['kind'] == 'control_drained' and item['sequence'] < frame['sequence']]
                    if len(prior_drains) != 1 or captured < self.utc_ticks(prior_drains[0]['observed_utc']):
                        raise ValueError('report binding')
                report = {key: data[key] for key in ('name', 'length', 'sha256')}
                if name not in files:
                    raise ValueError('b10_abrupt_publication_ambiguous')
                bound(report, name, files[name])
                if kind == 'pre_acceptance_report' and not leader_up(files[name], 'document-events', data['partition']):
                    raise ValueError('b10_abrupt_report_binding_invalid')
                reports.append(report)
            if kind in PRODUCING_KINDS:
                last_producing = frame
        if any(attempt not in accepted and attempt not in rejections for attempt in prepared):
            raise ValueError('prepared acceptance binding')
        if any(attempt not in accepted or attempt in rejections for attempt in terminals):
            raise ValueError('terminal binding')
        for arm in ('same_instance', 'cross_restart'):
            originals = [frame['data'] for frame in prepared.values() if frame['data']['arm'] == arm and frame['data']['role'] == 'original']
            if [data['ordinal'] for data in originals] != list(range(5)):
                raise ValueError('prepared binding')
            if any((data['payload_length'], data['payload_sha256']) !=
                   (originals[0]['payload_length'], originals[0]['payload_sha256']) for data in originals):
                raise ValueError('prepared binding')
        resends = [frame for frame in prepared.values() if frame['data']['role'] == 'resend']
        if cut is None or [frame['data']['predecessor_attempt_id'] for frame in resends] != control:
            raise ValueError('selection stale')
        for frame in resends:
            data = frame['data']
            original = prepared.get(data['predecessor_attempt_id'])
            if (data['arm'] != 'same_instance' or original is None
                    or any(data[key] != original['data'][key] for key in ('key', 'sequence', 'ordinal', 'probe_id', 'payload_length', 'payload_sha256'))
                    or self.utc_ticks(frame['observed_utc']) < self.utc_ticks(cut['data']['release_utc'])):
                raise ValueError('prepared binding')
        kill = [attempt for attempt in accepted if prepared[attempt]['data']['arm'] == 'cross_restart'
                and prepared[attempt]['data']['role'] == 'original' and attempt not in terminals and attempt not in rejections]
        producing = [frame for frame in frames if frame['kind'] in PRODUCING_KINDS]
        drains = [frame['data'] for frame in frames if frame['kind'] == 'control_drained']
        control_attempts = [attempt for attempt in accepted if prepared[attempt]['data']['arm'] == 'same_instance']
        if (cut is None or intent is None or not control or not kill or producing[-1] != intent
                or termination['control_unconfirmed_attempt_ids'] != control or termination['kill_unconfirmed_attempt_ids'] != kill
                or len(drains) != 1 or drains[0]['flush_returned'] is not True
                or type(drains[0]['out_queue_length']) is not int or drains[0]['out_queue_length'] != 0
                or any(attempt not in terminals for attempt in control_attempts)):
            raise ValueError('selection stale')
        if manifest['report_files'] != reports or [entry['name'] for entry in reports] != list(REPORT_FILES):
            raise ValueError('b10_abrupt_publication_failed')
        if set(files) != set(FIRST_FILES) | {entry['name'] for entry in reports}:
            raise ValueError('b10_abrupt_publication_ambiguous')
        self.accept_fatal_coverage(frames, termination)
        # Keep precisely the bytes accepted here for successor receipt checks.
        target = self.directory / observation['pod_name']
        target.mkdir(exist_ok=True)
        for name, raw in files.items():
            (target / name).write_bytes(raw)
        return {'first_pod': observation, 'termination': descriptor('termination.json', files['termination.json']),
                'release_utc': intent['data']['release_utc'],
                'start_line_binding': decode((self.p.private / 'b10-abrupt-start-line-binding.json').read_bytes()),
                'runner_utc': datetime.now(timezone.utc).strftime('%Y-%m-%dT%H:%M:%S.%f') + '0+00:00',
                'runner_monotonic_ticks': time.monotonic_ns(),
                'cross_host_timing': 'not_verified'}

    def accept_fatal_coverage(self, frames, termination):
        natives = [frame['data'] for frame in frames if frame['kind'] == 'native_error']
        count = len(natives)
        if any(type(data['capture_sequence']) is not int or type(data['is_fatal']) is not bool for data in natives):
            raise ValueError('b10_abrupt_publication_failed')
        if [data['capture_sequence'] for data in natives] != list(range(1, count + 1)):
            raise ValueError('b10_abrupt_publication_failed')
        fatal = sum(1 for data in natives if data['is_fatal'])
        if (type(termination['native_error_count']) is not int or termination['native_error_count'] != count
                or type(termination['native_fatal_count']) is not int or termination['native_fatal_count'] != fatal):
            raise ValueError('b10_abrupt_publication_failed')

    def release_successor(self, binding, deadline):
        if self.p.state().get('safety_stop', False):
            self.fail('b10_abrupt_first_termination_unknown', safety=True)
        _, ticks = self.p.b10_utc(binding['release_utc'])
        if time.time() * 10000000 >= ticks:
            self.fail('b10_abrupt_successor_late')
        self.configmap('successor', binding)
        if time.time() * 10000000 >= ticks:
            self.fail('b10_abrupt_successor_late')
        return self.job('loadgen-successor', deadline)

    def run(self):
        self.phase()
        self.directory.mkdir(exist_ok=True)
        state = self.p.state()
        if state.get('b10_abrupt_started'):
            self.fail('b10_abrupt_configuration_invalid')
        state['b10_abrupt_started'] = True
        self.p.save_state(state)
        previous = self.p.command_deadline
        deadline = self.p.b10_deadline(start=True)
        self.p.command_deadline = deadline
        boundary, failure = 'metadata', 'b10_abrupt_publication_failed'
        try:
            metadata = self.p.run_mode('start-line', 1, absolute_deadline=deadline)
            if metadata['outcome'] != 'complete' or self.p.state()['safety_stop']:
                raise ValueError('b10_abrupt_publication_failed')
            self.metadata(metadata, deadline)
            boundary, failure = 'timeout_job', 'b10_abrupt_publication_failed'
            timeout = self.job('loadgen', deadline)
            if timeout['outcome'] in ('complete', 'failed'):
                self._observe_timeout_termination(timeout)
            if timeout['outcome'] in ('unknown', 'apply_failed'):
                self.record_failure(ValueError('b10_abrupt_timeout_job_unknown'),
                                    'b10_abrupt_timeout_job_unknown', boundary, safety=True)
                return 1
            if timeout['outcome'] == 'render_failed':
                raise ValueError('b10_abrupt_configuration_invalid')
            boundary, failure = 'first_job', 'b10_abrupt_publication_failed'
            first = self.job('loadgen-restart', deadline)
            boundary, failure = 'first_termination', 'b10_abrupt_first_termination_unknown'
            observation = self.observe_first_termination(first)
            if first['outcome'] != 'complete':
                raise ValueError('b10_abrupt_publication_failed')
            boundary, failure = 'first_publication', 'b10_abrupt_publication_failed'
            files = self.download(observation, FIRST_FILES, deadline)
            manifest = self.envelope(files['abrupt-manifest.json'], 'abrupt_manifest', observation)['data']
            names = [entry['name'] for entry in manifest['report_files']]
            if len(names) > len(REPORT_FILES) or len(set(names)) != len(names) or not set(names) <= set(REPORT_FILES):
                raise ValueError('b10_abrupt_publication_failed')
            files.update(self.download(observation, names, deadline))
            binding = self.accept_diagnostic_publication(observation, files)
            (self.directory / 'predecessor-binding.json').write_bytes(encode(binding))
            boundary, failure = 'successor_admission', 'b10_abrupt_successor_binding_invalid'
            successor = self.release_successor(binding, deadline)
            if successor['outcome'] != 'complete':
                reason = ('b10_abrupt_deadline_exhausted' if successor.get('wait') == 124
                          else 'b10_abrupt_publication_ambiguous')
                self.record_failure(ValueError(reason), reason, 'successor_job')
                return 1
            return 0
        except (ValueError, OSError, LookupError, TypeError, AttributeError, self.failure) as error:
            self.record_failure(error, failure, boundary)
            return 1
        finally:
            self.p.command_deadline = previous

    def collect(self, files=None, observation=None):
        self.phase()
        self.directory.mkdir(exist_ok=True)
        binding_path = self.directory / 'predecessor-binding.json'
        if files is not None:
            binding = decode(binding_path.read_bytes())
            first = binding['first_pod']
            if observation['job_name'] != self.phase()['run_id'] + '-c1-r1-loadgen-successor':
                raise ValueError('b10_abrupt_successor_binding_invalid')
            if observation['pod_name'] == first['pod_name'] or observation['pod_uid'] == first['pod_uid']:
                raise ValueError('b10_abrupt_successor_binding_invalid')
            receipt = self.envelope(files['completion.json'], 'successor_receipt', observation, first['pod_name'])['data']
            if set(files) != set(SUCCESSOR_FILES) or receipt['cleanup_completed'] is not True:
                raise ValueError('b10_abrupt_publication_failed')
            bound(receipt['registration'], 'registration.json', files['registration.json'])
            if {entry['name'] for entry in receipt['artifacts']} != set(SUCCESSOR_FILES) - {'registration.json', 'completion.json'} or len(receipt['artifacts']) != 5:
                raise ValueError('successor artifact set')
            for entry in receipt['artifacts']:
                bound(entry, entry['name'], files[entry['name']])
            target = self.directory / first['pod_name']
            accepted_files = {name: (target / name).read_bytes() for name in ('termination.json', 'journal.jsonl')}
            for key, name in [('first_termination', 'termination.json'), ('journal', 'journal.jsonl')]:
                bound(receipt[key], name, accepted_files[name])
            bound(binding['termination'], 'termination.json', accepted_files['termination.json'])
            if receipt['release_utc'] != binding['release_utc'] or receipt['cross_host_timing'] != 'not_verified':
                raise ValueError('successor release')
            self.envelope(files['registration.json'], 'registration', observation, first['pod_name'])
            ready = self.utc_ticks(receipt['ready_utc'])
            release = self.utc_ticks(receipt['release_utc'])
            selected = decode(accepted_files['termination.json'])['data']['kill_unconfirmed_attempt_ids']
            if receipt['kill_unconfirmed_attempt_ids'] != selected:
                raise ValueError('b10_abrupt_successor_binding_invalid')
            prepared = {frame['data']['attempt_id']: frame['data'] for frame in
                        self.read_journal(accepted_files['journal.jsonl'], first) if frame['kind'] == 'prepared'}
            invocations = receipt['invocations']
            if ready > release or len(invocations) != len(selected):
                raise ValueError('b10_abrupt_successor_late')
            for member, invocation in zip(selected, invocations):
                original = prepared[member]
                if (invocation['predecessor_attempt_id'] != member or invocation['attempt_id'] == member
                        or any(invocation[key] != original[key] for key in ('key', 'sequence', 'payload_sha256'))
                        or self.utc_ticks(invocation['invocation_utc']) < release):
                    raise ValueError('b10_abrupt_successor_binding_invalid')
            return receipt
        failed = False
        for record in self.p.state()['modes']:
            if record['mode'] not in ('loadgen', 'loadgen-restart', 'loadgen-successor') or record['apply'] is None:
                continue
            boundary, failure = 'successor_termination', 'b10_abrupt_first_termination_unknown'
            try:
                if record['mode'] == 'loadgen-successor':
                    observation = self.observe_first_termination(record)
                    boundary, failure = 'successor_publication', 'b10_abrupt_publication_failed'
                    files = self.download(observation, SUCCESSOR_FILES, self.p.b10_deadline())
                    boundary, failure = 'successor_receipt', 'b10_abrupt_successor_binding_invalid'
                    self.collect(files, observation)
            except (ValueError, OSError, LookupError, TypeError, AttributeError, self.failure) as error:
                self.record_failure(error, failure, boundary)
                failed = True
            try:
                code = self.p.run(['bash', 'infra/scripts/lifecycle/lifecycle.sh', 'collect-job', record['job']], 210)
                if code:
                    reason = ('b10_abrupt_deadline_exhausted' if code == 124
                              else 'b10_abrupt_publication_ambiguous')
                    self.record_failure(ValueError(reason), reason, 'job_collection')
                    failed = True
                source = self.p.artifacts / record['job']
                if source.is_dir():
                    shutil.copytree(source, self.directory / record['job'], dirs_exist_ok=True)
            except (ValueError, OSError, LookupError, TypeError, AttributeError, self.failure) as error:
                self.record_failure(error, 'b10_abrupt_publication_failed', 'job_collection')
                failed = True
        try:
            (self.directory / 'phase-status.json').write_bytes(encode(self.p.state()))
        except (ValueError, OSError, LookupError, TypeError, AttributeError, self.failure) as error:
            self.record_failure(error, 'b10_abrupt_publication_failed', 'diagnostic_status')
            failed = True
        return int(failed)

"""Bounded diagnostic-only orchestration for the owned-child restart trial."""
import base64
from datetime import datetime, timezone
import hashlib
import json
import os
import re
import shutil
import time

AUXILIARY_MAXIMUM = 65536
JOURNAL_MAXIMUM = 16777216
FIRST_FILES = ('registration.json', 'journal.jsonl', 'timeout-seal.json', 'control-seal.json',
               'kill-selection.json', 'abrupt-manifest.json', 'termination.json')
SUCCESSOR_FILES = ('registration.json', 'ledger.jsonl', 'manifest.json', 'statistics.jsonl',
                   'request-lifecycle.jsonl', 'capabilities.json', 'completion.json')
IDENTITY_FIELDS = set('run_id scenario_id configuration_id phase repeat source_commit image_reference job_name pod_name pod_uid instance_id predecessor_instance_id'.split())
ENVELOPE_FIELDS = set('schema_version dialect purpose synthetic scientific_admissible kind identity data'.split())

DATA_FIELDS = {key: set(value.split()) for key, value in {
    'registration': 'registered_utc',
    'prepared': 'attempt_id invocation_ordinal cohort probe_id key sequence payload_length payload_sha256 predecessor_attempt_id',
    'accepted': 'attempt_id prepared_sequence',
    'synchronous_rejection': 'attempt_id prepared_sequence error_code error_reason',
    'terminal': 'attempt_id completion_source delivered error_code persistence_status',
    'checkpoint': 'arm accepted_attempt_ids selected_attempt_ids last_complete_sequence pre_acceptance_report process checkpoint_utc checkpoint_monotonic_ticks release_utc',
    'signal': 'arm requested_signal process intent_sequence request_utc request_monotonic_ticks return_value errno',
    'exit': 'process wait_for_exit_observed observed_utc monotonic_ticks exit_code_local_observation runtime_version',
    'gap': 'reason first_missing_sequence capture_status',
    'selection': 'checkpoint checkpoint_descriptor candidate_attempt_ids terminal_exclusions entries',
    'cohort_seal': 'cohort inventory count ledger drain_complete post_drain_statistics guard_complete fatal_capture_status',
    'abrupt_manifest': 'journal journal_entry_count timeout_seal control_seal selection gaps registration',
    'termination': 'manifest process signals exit cgroup_before cgroup_after eof_complete mechanical_status fatal_capture_status native_tail',
    'successor_receipt': 'first_termination selection registration artifacts ready_utc release_utc invocations cleanup_completed cross_host_timing',
}.items()}

# Emitted outcomes are the closed list in the approved diagnostic contract.
FAILURE_NAMES = frozenset('''
b10_abrupt_diagnostic_not_scientific
b10_abrupt_configuration_invalid
b10_abrupt_platform_unsupported
b10_abrupt_signal_preflight_failed
b10_abrupt_child_start_failed
b10_abrupt_child_identity_mismatch
b10_abrupt_target_invalid
b10_abrupt_signal_failed
b10_abrupt_stop_unconfirmed
b10_abrupt_resume_unconfirmed
b10_abrupt_exit_unobserved
b10_abrupt_eof_incomplete
b10_abrupt_unexpected_exit
b10_abrupt_capture_gap
b10_abrupt_sample_missing
b10_abrupt_report_binding_invalid
b10_abrupt_guard_failed
b10_abrupt_control_incomplete
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
'''.split())


class RecordedFailure(ValueError):
    """A failure already retained in phase-status.json by an inner boundary."""


def execution_modes(configuration):
    if configuration == 'C0':
        return ('loadgen',)
    if configuration == 'C1':
        return ('loadgen', 'loadgen2')
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


class Trial:
    def __init__(self, phase, failure=ValueError):
        self.p = phase
        self.failure = failure

    @property
    def directory(self):
        return self.p.artifacts / 'b10-abrupt-diagnostic'

    def record_failure(self, error, fallback, boundary, safety=False):
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
            'validation_detail': message})
        state['candidate'] = None
        state['safety_stop'] = (state.get('safety_stop', False) or safety
                                or reason == 'b10_abrupt_first_termination_unknown')
        self.p.save_state(state)

    def fail(self, reason, safety=False):
        self.record_failure(ValueError(reason), reason, 'admission', safety)
        raise RecordedFailure(reason)

    def prepare(self):
        expected = {'PARCHWELL_SCENARIO': 'B10', 'PARCHWELL_PHASE': 'C1',
                    'PARCHWELL_CONFIGURATION': 'C1', 'PARCHWELL_REPEAT': '1',
                    'PARCHWELL_RUN_KIND': 'smoke', 'PARCHWELL_PURPOSE': 'diagnostic_trial',
                    'PARCHWELL_B10_ABRUPT_DIAGNOSTIC': 'true', 'PARCHWELL_CONFIRM_DIAGNOSTIC_TRIAL': 'true'}
        if (any(os.environ.get(key) != value for key, value in expected.items())
                or any(key.lower().startswith('b10abrupt__') for key in os.environ)):
            raise self.failure('b10_abrupt_configuration_invalid')
        return self.p.prepare_shared('B10', 'C1', 'C1', 'b10', purpose='diagnostic_trial',
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
        role = 'first' if mode == 'loadgen' else 'successor'
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
        except (ValueError, OSError, LookupError, TypeError, AttributeError):
            self.fail('b10_abrupt_first_termination_unknown', safety=True)

    def envelope(self, raw, kind, observation, predecessor=None, maximum=AUXILIARY_MAXIMUM):
        value = decode(raw, maximum)
        if (set(value) != ENVELOPE_FIELDS or type(value['schema_version']) is not int or value['schema_version'] != 2
                or value['dialect'] != 'b10_abrupt' or value['purpose'] != 'diagnostic_trial'
                or value['synthetic'] is not True or value['scientific_admissible'] is not False or value['kind'] != kind):
            raise ValueError('diagnostic envelope')
        identity = value['identity']
        phase = self.phase()
        expected = {'run_id': phase['run_id'], 'scenario_id': 'B10', 'configuration_id': 'C1', 'phase': 'C1',
                    'repeat': 1, 'source_commit': phase['source_commit'], 'image_reference': phase['images']['loadgen']['reference'],
                    'job_name': observation['job_name'], 'pod_name': observation['pod_name'], 'pod_uid': observation['pod_uid'],
                    'instance_id': observation['pod_name'], 'predecessor_instance_id': predecessor}
        if set(identity) != IDENTITY_FIELDS or identity != expected or type(identity['repeat']) is not int:
            raise ValueError('diagnostic identity')
        if kind not in DATA_FIELDS or not isinstance(value['data'], dict) or set(value['data']) != DATA_FIELDS[kind]:
            raise ValueError('diagnostic data members')
        return value

    def download(self, observation, names, deadline):
        phase = self.phase()
        prefix = phase['run_id'] + '/C1/r1/b10-abrupt-diagnostic/' + observation['pod_name'] + '/'
        target = self.directory / observation['pod_name']
        target.mkdir(exist_ok=True)
        files = {}
        for name in names:
            limits = {'journal.jsonl': JOURNAL_MAXIMUM, 'ledger.jsonl': 268435456, 'manifest.json': 67108864,
                      'statistics.jsonl': 134217728, 'request-lifecycle.jsonl': 134217728}
            limit = min(limits.get(name, AUXILIARY_MAXIMUM), 1073741824 - sum(map(len, files.values())))
            raw = self.p.download_blob(prefix + name, target / name, limit, deadline, container='ledger', minimum=0 if name == 'request-lifecycle.jsonl' else 1)
            if raw is None:
                raise ValueError('b10_abrupt_publication_ambiguous')
            files[name] = raw
        if sum(map(len, files.values())) > 1073741824:
            raise ValueError('b10_abrupt_publication_failed')
        return files

    def accept_diagnostic_publication(self, observation, files):
        if set(files) != set(FIRST_FILES):
            raise ValueError('b10_abrupt_publication_ambiguous')
        receipt_identity = decode(files['termination.json'])['identity']
        if any(receipt_identity.get(key) != observation.get(key) for key in ('job_name', 'pod_name', 'pod_uid')):
            self.fail('b10_abrupt_first_termination_unknown', safety=True)
        kinds = {'registration.json': 'registration', 'timeout-seal.json': 'cohort_seal', 'control-seal.json': 'cohort_seal',
                 'kill-selection.json': 'selection', 'abrupt-manifest.json': 'abrupt_manifest', 'termination.json': 'termination'}
        objects = {name: self.envelope(files[name], kind, observation) for name, kind in kinds.items()}
        termination, manifest, selection = (objects[name]['data'] for name in ('termination.json', 'abrupt-manifest.json', 'kill-selection.json'))
        bound(termination['manifest'], 'abrupt-manifest.json', files['abrupt-manifest.json'])
        for key, name in [('registration', 'registration.json'), ('journal', 'journal.jsonl'), ('timeout_seal', 'timeout-seal.json'),
                          ('control_seal', 'control-seal.json'), ('selection', 'kill-selection.json')]:
            bound(manifest[key], name, files[name])
        if (manifest['gaps'] != [] or termination['mechanical_status'] != 'complete'
                or termination['eof_complete'] is not True or termination['fatal_capture_status'] != 'not_implemented_pr3'
                or termination['native_tail'] != 'unknown_due_to_intended_kill'):
            raise ValueError('b10_abrupt_publication_failed')
        for name, cohort in [('timeout-seal.json', 'timeout'), ('control-seal.json', 'same_instance')]:
            seal = objects[name]['data']
            if seal['cohort'] != cohort or seal['drain_complete'] is not True or seal['guard_complete'] is not True:
                raise ValueError('b10_abrupt_control_incomplete')
        before, after = termination['cgroup_before'], termination['cgroup_after']
        cgroup_fields = {'boot_id', 'cgroup_path', 'directory_identity', 'oom_kill'}
        if (not isinstance(before, dict) or not isinstance(after, dict) or set(before) != cgroup_fields
                or set(after) != cgroup_fields or before != after or type(before['oom_kill']) is not int
                or not 0 <= before['oom_kill'] <= 9223372036854775807):
            raise ValueError('b10_abrupt_oom_conflict')
        if (before['boot_id'] != termination['process'].get('boot_id')
                or any(not isinstance(before[key], str) or not before[key] for key in ('boot_id', 'cgroup_path', 'directory_identity'))):
            raise ValueError('b10_abrupt_cgroup_unavailable')
        if termination['exit'].get('wait_for_exit_observed') is not True:
            raise ValueError('b10_abrupt_first_termination_unknown')
        if len(files['journal.jsonl']) > JOURNAL_MAXIMUM:
            raise ValueError('journal size')
        if files['journal.jsonl'].count(b'\n') > 4096:
            raise ValueError('journal entry bound')
        lines = files['journal.jsonl'].splitlines()
        if not 0 < len(lines) <= 4096 or type(manifest['journal_entry_count']) is not int or manifest['journal_entry_count'] != len(lines):
            raise ValueError('journal count')
        frames = [decode(line, JOURNAL_MAXIMUM) for line in lines]
        for sequence, frame in enumerate(frames, 1):
            if (set(frame) != ENVELOPE_FIELDS | {'sequence', 'observed_utc', 'monotonic_ticks', 'clock_frequency'}
                    or type(frame['sequence']) is not int or frame['sequence'] != sequence
                    or frame['identity'] != objects['termination.json']['identity']):
                raise ValueError('journal sequence or identity')
            if any(type(frame[key]) is not int or not 0 <= frame[key] <= 9223372036854775807 for key in ('monotonic_ticks', 'clock_frequency')) or frame['clock_frequency'] == 0:
                raise ValueError('journal clock')
            self.p.b10_utc(frame['observed_utc'])
            envelope = {key: frame[key] for key in ENVELOPE_FIELDS}
            self.envelope(encode(envelope), frame['kind'], observation, maximum=JOURNAL_MAXIMUM)
        for frame in frames:
            if frame['kind'] == 'gap':
                raise ValueError('journal capture gap')
            if frame['kind'] == 'checkpoint':
                report = frame['data']['pre_acceptance_report']
                if set(report) != {'name', 'length', 'sha256', 'report_identity', 'report_sequence', 'report_capture_utc'}:
                    raise ValueError('report descriptor')
                report_identity = report['report_identity']
                if (set(report_identity) != {'scenario_id', 'run_id', 'role', 'client_id'}
                        or report_identity['scenario_id'] != 'B10' or report_identity['run_id'] != observation.get('run_id', self.phase()['run_id'])
                        or report_identity['role'] != 'LoadGenProducer' or not isinstance(report_identity['client_id'], str)
                        or not report_identity['client_id'] or type(report['report_sequence']) is not int
                        or not 1 <= report['report_sequence'] <= 9223372036854775807
                        or type(report['length']) is not int or not 1 <= report['length'] <= 1048576
                        or not re.fullmatch('[0-9a-f]{64}', report['sha256'])
                        or report['name'] != 'abrupt-child/' + frame['data']['arm'] + '-pre-acceptance-report.json'
                        or not isinstance(report['report_capture_utc'], str) or len(report['report_capture_utc']) != 33
                        or self.p.b10_utc(report['report_capture_utc'])[0] != report['report_capture_utc']):
                    raise ValueError('report binding')
        signals = [frame for frame in frames if frame['kind'] == 'signal']
        if termination['signals'] != [frame['data'] for frame in signals]:
            raise ValueError('signal inventory')
        if [(frame['data']['arm'], frame['data']['requested_signal']) for frame in signals] != [
                ('same_instance', 19), ('same_instance', 18), ('cross_restart', 19), ('cross_restart', 9), ('cross_restart', 9)]:
            raise ValueError('signal order')
        for index, frame in enumerate(signals):
            data = frame['data']
            if (data['process'] != termination['process'] or data['return_value'] != (None if index == 3 else 0)
                    or index != 3 and type(data['return_value']) is not int):
                raise ValueError('signal result')
        if signals[4]['data']['intent_sequence'] != signals[3]['sequence']:
            raise ValueError('kill intent')
        checkpoints = [frame for frame in frames if frame['kind'] == 'checkpoint']
        if ([frame['data']['arm'] for frame in checkpoints] != ['same_instance', 'cross_restart']
                or not signals[0]['sequence'] < checkpoints[0]['sequence'] < signals[1]['sequence']
                or not signals[2]['sequence'] < checkpoints[1]['sequence'] < signals[3]['sequence']):
            raise ValueError('checkpoint signal order')
        exits = [frame for frame in frames if frame['kind'] == 'exit']
        if (len(exits) != 1 or exits[0]['sequence'] <= signals[-1]['sequence']
                or exits[0]['data']['wait_for_exit_observed'] is not True
                or exits[0]['data']['process'] != termination['process']):
            raise ValueError('observed exit')
        checkpoint = selection['checkpoint']
        if checkpoint not in frames or checkpoint['kind'] != 'checkpoint' or checkpoint['data']['arm'] != 'cross_restart':
            raise ValueError('selection checkpoint')
        checkpoint_bytes = lines[frames.index(checkpoint)]
        bound(selection['checkpoint_descriptor'], 'cross_restart-checkpoint.json', checkpoint_bytes)
        if len(selection['entries']) != 1:
            raise ValueError('kill_too_late')
        entry = selection['entries'][0]
        prepared = {frame['data']['attempt_id']: frame for frame in frames if frame['kind'] == 'prepared'}
        accepted_frames = [frame for frame in frames if frame['kind'] == 'accepted']
        accepted = {frame['data']['attempt_id'] for frame in accepted_frames}
        if len(accepted) != len(accepted_frames):
            raise ValueError('duplicate acceptance')
        for frame in accepted_frames:
            source = prepared.get(frame['data']['attempt_id'])
            if source is None or source['sequence'] != frame['data']['prepared_sequence'] or source['sequence'] >= frame['sequence']:
                raise ValueError('prepared acceptance binding')
        terminal = {frame['data']['attempt_id'] for frame in frames if frame['kind'] == 'terminal'}
        if entry['attempt_id'] not in accepted or entry['attempt_id'] in terminal or entry['attempt_id'] not in checkpoint['data']['selected_attempt_ids']:
            raise ValueError('selection stale')
        if (set(entry) != {'attempt_id', 'key', 'sequence', 'payload_length', 'payload_sha256', 'payload_base64'}
                or entry['key'] != 'B10/' + self.phase()['run_id'] + '/C1/1/cross_restart'
                or type(entry['sequence']) is not int or entry['sequence'] != 0
                or selection['candidate_attempt_ids'] != checkpoint['data']['selected_attempt_ids']):
            raise ValueError('selection identity')
        original = prepared[entry['attempt_id']]['data']
        if (original['cohort'] != 'cross_restart' or any(original[key] != entry[key] for key in ('key', 'sequence', 'payload_length', 'payload_sha256'))
                or selection['terminal_exclusions'] != []):
            raise ValueError('prepared selection binding')
        payload = base64.b64decode(entry['payload_base64'], validate=True)
        if len(payload) != entry['payload_length'] or hashlib.sha256(payload).hexdigest() != entry['payload_sha256']:
            raise ValueError('selection payload')
        _, checkpoint_ticks = self.p.b10_utc(checkpoint['data']['checkpoint_utc'])
        release, release_ticks = self.p.b10_utc(checkpoint['data']['release_utc'])
        if release_ticks - checkpoint_ticks != 120 * 10000000:
            raise ValueError('release boundary')
        return {'files': {name: raw.decode() for name, raw in files.items() if name != 'journal.jsonl'},
                'first_pod': observation, 'release_utc': release,
                'start_line_binding': decode((self.p.private / 'b10-abrupt-start-line-binding.json').read_bytes()),
                'runner_utc': datetime.now(timezone.utc).isoformat(), 'runner_monotonic_ticks': time.monotonic_ns(),
                'cross_host_timing': 'not_verified'}

    def release_successor(self, binding, deadline):
        if self.p.state().get('safety_stop', False):
            self.fail('b10_abrupt_first_termination_unknown', safety=True)
        _, ticks = self.p.b10_utc(binding['release_utc'])
        if time.time() * 10000000 >= ticks:
            self.fail('b10_abrupt_successor_late')
        self.configmap('successor', binding)
        if time.time() * 10000000 >= ticks:
            self.fail('b10_abrupt_successor_late')
        return self.job('loadgen2', deadline)

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
            boundary, failure = 'first_job', 'b10_abrupt_publication_failed'
            first = self.job('loadgen', deadline)
            boundary, failure = 'first_termination', 'b10_abrupt_first_termination_unknown'
            observation = self.observe_first_termination(first)
            if first['outcome'] != 'complete':
                raise ValueError('b10_abrupt_publication_failed')
            boundary, failure = 'first_publication', 'b10_abrupt_publication_failed'
            files = self.download(observation, FIRST_FILES, deadline)
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
            for key, name in [('first_termination', 'termination.json'), ('selection', 'kill-selection.json')]:
                bound(receipt[key], name, binding['files'][name].encode())
            if receipt['release_utc'] != binding['release_utc'] or receipt['cross_host_timing'] != 'not_verified':
                raise ValueError('successor release')
            self.envelope(files['registration.json'], 'registration', observation, first['pod_name'])
            _, ready = self.p.b10_utc(receipt['ready_utc'])
            _, release = self.p.b10_utc(receipt['release_utc'])
            selected = decode(binding['files']['kill-selection.json'].encode())['data']['entries']
            invocations = receipt['invocations']
            if ready > release or len(invocations) != len(selected):
                raise ValueError('b10_abrupt_successor_late')
            for original, invocation in zip(selected, invocations):
                if (invocation['predecessor_attempt_id'] != original['attempt_id'] or invocation['attempt_id'] == original['attempt_id']
                        or any(invocation[key] != original[key] for key in ('key', 'sequence', 'payload_sha256'))
                        or self.p.b10_utc(invocation['invocation_utc'])[1] < release):
                    raise ValueError('b10_abrupt_successor_binding_invalid')
            return receipt
        failed = False
        for record in self.p.state()['modes']:
            if record['mode'] not in ('loadgen', 'loadgen2') or record['apply'] is None:
                continue
            boundary, failure = 'successor_termination', 'b10_abrupt_first_termination_unknown'
            try:
                if record['mode'] == 'loadgen2':
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

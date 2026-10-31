#!/usr/bin/env python3
"""Check actual .NET test exports against Python transport, without a live service."""
import argparse
import json
from pathlib import Path
import sys
import time
from unittest.mock import patch

from test_probe_v2 import ProbeV2Tests


def check_actor(directory):
    case = ProbeV2Tests()
    case.setUp()
    try:
        identity = json.loads((directory / 'registration.json').read_bytes())
        envelope = json.loads((directory / 'pod.json').read_bytes())
        phase = case.p.read('phase.json')
        phase.update(run_id=identity['run_id'], source_commit=identity['source_commit'])
        phase['images']['loadgen'] = {'digest': envelope['image_digest'], 'reference': 'synthetic.azurecr.io/parchwell/loadgen@' + envelope['image_digest']}
        case.write(case.private / 'phase.json', phase)
        record = {'job': identity['job_name'], 'item': identity['scenario_id'], 'configuration': identity['configuration_id'],
                  'stage': identity['stage'], 'role': identity['service_account'], 'outcome': 'complete', 'collect': 0}
        path = case.artifacts / record['job']
        path.mkdir()
        pods = {'items': [{'metadata': {'name': envelope['pod_name'], 'uid': envelope['pod_uid'],
            'ownerReferences': [{'controller': True, 'kind': 'Job', 'name': envelope['owner_name']}]},
            'spec': {'serviceAccountName': envelope['service_account'], 'containers': [{'name': record['role'], 'image': phase['images']['loadgen']['reference']}]},
            'status': {'containerStatuses': [{'name': record['role'], 'imageID': phase['images']['loadgen']['reference'], 'state': {'terminated': {'exitCode': 0}}}]}}]}
        source_log = (directory / 'actor.stdout').read_bytes()
        maximum_log = case.subject.limits['execution']['max_container_log_bytes']
        def collect(args, seconds, output=None, maximum=4194304):
            if args[1:3] == ['get', 'pods']:
                case.write(output, pods)
                return 0
            if args[1] != 'logs' or f'--limit-bytes={maximum_log + 1}' not in args or maximum != maximum_log:
                raise AssertionError('actor collection did not use the configured log budget')
            output.write_bytes(source_log[:maximum])
            return 125 if len(source_log) > maximum else 0
        with patch.object(case.p, 'run', side_effect=collect):
            if case.subject.collect(record) != (0, True):
                raise AssertionError('actual actor log rejected by collection')
        collected = path / f"{envelope['pod_name']}.containers.{record['role']}.log"
        if collected.read_bytes() != source_log:
            raise AssertionError('actual actor log changed by collection')
        with patch.object(case.p, 'run', side_effect=case.fake):
            if not case.subject.transfer(record):
                raise AssertionError('actual actor bytes rejected by transport')
        payload = case.transported(record)
        if len(case.calls) != 4 or any(call[0][1] != 'create' for call in case.calls):
            raise AssertionError('actor transport did not create exactly four immutable parts')
        for name in ('registration.json', 'capture.json'):
            if payload[name] != (directory / name).read_bytes().rstrip(b'\r\n'):
                raise AssertionError('actor bytes changed by transport: ' + name)
    finally:
        case.doCleanups()


def check_judge(directory, run_kind="smoke"):
    case = ProbeV2Tests()
    case.setUp()
    try:
        candidate_bytes = (directory / 'candidate.json').read_bytes()
        receipt_bytes = (directory / 'judge-completion.json').read_bytes()
        candidate, receipt = json.loads(candidate_bytes), json.loads(receipt_bytes)
        phase = case.p.read('phase.json')
        phase.update(run_id=candidate['run_id'], source_commit=candidate['source_commit'], run_kind=run_kind)
        phase['images'] = {name: {'digest': digest, 'reference': 'synthetic.azurecr.io/parchwell/' + name + '@' + digest} for name, digest in candidate['image_digests'].items()}
        case.write(case.private / 'phase.json', phase)
        record = {'job': receipt['job_name'], 'item': candidate['scenario_id'], 'configuration': candidate['configuration_id'],
                  'stage': 'judge', 'role': 'verifier', 'outcome': 'complete', 'collect': 0}
        path = case.artifacts / record['job']; path.mkdir()
        case.write(path / 'pods.json', {'items': [{'metadata': {'name': receipt['pod_name'], 'uid': receipt['pod_uid'],
            'ownerReferences': [{'controller': True, 'kind': 'Job', 'name': record['job']}]},
            'spec': {'serviceAccountName': 'verifier', 'containers': [{'name': 'verifier', 'image': phase['images']['verifier']['reference']}]},
            'status': {'containerStatuses': [{'name': 'verifier', 'imageID': phase['images']['verifier']['reference'], 'state': {'terminated': {'exitCode': 0}}}]}}]})
        bodies = iter((candidate_bytes, receipt_bytes))
        def download(_name, target, _maximum, _deadline):
            body = next(bodies); target.write_bytes(body); return body
        with patch.object(case.p, 'download_blob', side_effect=download):
            case.subject.candidate(record, time.monotonic() + 120)
        result = case.p.state()['candidates'][-1]
        if result['accepted'] is not True or result['smoke_valid' if run_kind == 'smoke' else 'evidence_valid'] is not True:
            raise AssertionError('actual happy-path judge candidate/receipt failed smoke transport')
    finally:
        case.doCleanups()


def check_metadata_actor(directory):
    directory = Path(directory)
    lines = (directory / 'actor.stdout').read_text(encoding='utf-8').splitlines()
    marker = 'PARCHWELL_PROBE_V2_AUTHENTICATION_METADATA '
    payloads = [line[len(marker):] for line in lines if line.startswith(marker)]
    if len(payloads) != 5:
        raise AssertionError('metadata actor must contain exactly five diagnostic rows')
    expected_boundaries = ['before_client', 'after_client_cleanup']
    expected_expiry = '2030-01-01T01:00:00+00:00'
    for repeat, payload in enumerate(payloads, 1):
        row = json.loads(payload)
        if row.get('schema_version') != 1 or row.get('mode') != 'single-action-v2-authentication-metadata':
            raise AssertionError('metadata actor schema or mode changed')
        if row.get('repeat') != repeat or row.get('initial_installed_expires_on') != expected_expiry:
            raise AssertionError('metadata actor repeat or fixed expiry changed')
        if [sample.get('boundary') for sample in row.get('projected_file_samples', [])] != expected_boundaries:
            raise AssertionError('metadata actor boundaries changed')

    # Inspect serialized record fields only; settings below them contain native Kafka keys.
    def is_diagnostic_field(key):
        lowered = key.lower()
        return ('authentication' in lowered or
                ('metadata' in lowered and lowered != 'metadata_partitions') or
                lowered in {'initial_installed_expires_on', 'lifecycle',
                            'projected_file_samples', 'client_cleanup_completed', 'capture_error'})

    for name in ('registration.json', 'capture.json'):
        scientific = json.loads((directory / name).read_bytes())
        if any(is_diagnostic_field(key) for key in scientific):
            raise AssertionError('diagnostic fields leaked into scientific ' + name)
        if name == 'capture.json' and any(
            is_diagnostic_field(key) for repeat in scientific['repeats'] for key in repeat
        ):
            raise AssertionError('diagnostic fields leaked into scientific repeats')


def check_handler_refresh_actor_export(directory):
    directory = Path(directory)
    expected_identity = {
        'producer': ('F06', 'C6', 'seed', 'loadgen'),
        'consumer': ('F06', 'C9', 'action', 'enrichment-consumer'),
    }
    metadata_fields = set('test_expiry_cap_ms observation_limit_ms started_at ended_at operation_admitted_at seek_returned_at started_timestamp ended_timestamp operation_admitted_timestamp seek_returned_timestamp timestamp_frequency operation_attempted trigger_attempt before_service after_service diagnostic_nonmatches diagnostic_errors observation_clock_unavailable capture_limit_reached duplicate_before_observation duplicate_after_observation cancelled operation_exception_type client_cleanup_completed'.split())
    service_fields = set('action client_id repeat topic partition offset observed_at timestamp persistence_status error_code error_is_fatal key_base64 value_base64'.split())
    registration_fields = set('schema_version mode synthetic run_id scenario_id configuration_id stage source_commit job_name pod_name pod_uid service_account'.split())
    capture_fields = set('schema_version mode synthetic run_id scenario_id configuration_id stage source_commit job_name pod_name pod_uid service_account managed_version native_version repeats handler_exit_code cleanup_completed host_cleanup_completed'.split())
    repeat_fields = set('repeat admission client_id action terminal elapsed_ms terminal_unix_ms settings omitted_keys metadata_partitions requested delivery read_observation result error client_errors statistics diagnostics'.split())
    for actor, identity in expected_identity.items():
        path = directory / actor
        if not path.is_dir():
            raise AssertionError('handler refresh actor export missing ' + actor)
        check_actor(path)
        registration = json.loads((path / 'registration.json').read_bytes())
        capture = json.loads((path / 'capture.json').read_bytes())
        if set(registration) != registration_fields or set(capture) != capture_fields:
            raise AssertionError('handler refresh scientific root fields changed')
        if (registration['scenario_id'], registration['configuration_id'], registration['stage'], registration['service_account']) != identity:
            raise AssertionError('handler refresh actor identity changed')
        if any(set(row) != repeat_fields for row in capture['repeats']):
            raise AssertionError('handler refresh scientific repeat fields changed')
        lines = (path / 'actor.stdout').read_text(encoding='utf-8').splitlines()
        marker = 'PARCHWELL_PROBE_V2_AUTHENTICATION_METADATA '
        payloads = [line[len(marker):] for line in lines if line.startswith(marker)]
        if len(payloads) != 5:
            raise AssertionError('handler refresh actor must contain exactly five metadata rows')
        for repeat, payload in enumerate(payloads, 1):
            row = json.loads(payload)
            if row.get('schema_version') != 2 or row.get('mode') != 'single-action-v2-handler-refresh-path':
                raise AssertionError('handler refresh metadata mode or schema changed')
            if (row.get('scenario_id'), row.get('configuration_id'), row.get('stage'), row.get('service_account'), row.get('repeat')) != (*identity, repeat):
                raise AssertionError('handler refresh metadata identity or repeat changed')
            diagnostic = row.get('handler_refresh')
            if not isinstance(diagnostic, dict) or set(diagnostic) != metadata_fields:
                raise AssertionError('handler refresh raw metadata fields changed')
            if diagnostic.get('test_expiry_cap_ms') != 5000 or diagnostic.get('observation_limit_ms') != 20000:
                raise AssertionError('handler refresh cap or observation boundary changed')
            if type(diagnostic.get('operation_attempted')) is not bool or diagnostic.get('trigger_attempt') != 3:
                raise AssertionError('handler refresh attempt bookkeeping changed')
            if type(diagnostic.get('client_cleanup_completed')) is not bool:
                raise AssertionError('handler refresh cleanup bookkeeping changed')
            for name in ('before_service', 'after_service'):
                observation = diagnostic.get(name)
                if observation is not None and set(observation) != service_fields:
                    raise AssertionError('handler refresh service observation fields changed')
            if len(diagnostic.get('diagnostic_errors', [])) > 8:
                raise AssertionError('handler refresh diagnostic error bound exceeded')
            if any(key in diagnostic for key in ('complete', 'exact_match', 'pass', 'incomplete_reason')):
                raise AssertionError('handler refresh raw payload contains a semantic judgement')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('actor_export', type=Path)
    parser.add_argument('judge_export', type=Path)
    parser.add_argument('metadata_actor_export', type=Path, nargs='?')
    parser.add_argument('--batch-judge-export', type=Path)
    parser.add_argument('--handler-refresh-actor-export', type=Path)
    args = parser.parse_args()
    check_actor(args.actor_export)
    check_judge(args.judge_export)
    if args.metadata_actor_export:
        check_metadata_actor(args.metadata_actor_export)
        check_actor(args.metadata_actor_export)
    if args.batch_judge_export:
        check_judge(args.batch_judge_export, 'batch')
    if args.handler_refresh_actor_export:
        check_handler_refresh_actor_export(args.handler_refresh_actor_export)
    print('{"probe_v2_actual_producer_transport_valid":true}')

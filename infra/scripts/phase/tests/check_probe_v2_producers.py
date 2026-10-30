#!/usr/bin/env python3
"""Check actual .NET test exports against Python transport, without a live service."""
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


def check_judge(directory):
    case = ProbeV2Tests()
    case.setUp()
    try:
        candidate_bytes = (directory / 'candidate.json').read_bytes()
        receipt_bytes = (directory / 'judge-completion.json').read_bytes()
        candidate, receipt = json.loads(candidate_bytes), json.loads(receipt_bytes)
        phase = case.p.read('phase.json')
        phase.update(run_id=candidate['run_id'], source_commit=candidate['source_commit'])
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
        if result['accepted'] is not True or result['smoke_valid'] is not True:
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
    for name in ('registration.json', 'capture.json'):
        scientific = json.loads((directory / name).read_bytes())
        if any('authentication' in key.lower() or 'metadata' in key.lower() for key in scientific):
            raise AssertionError('diagnostic fields leaked into scientific ' + name)
        if name == 'capture.json' and any(
            'authentication' in key.lower() or 'metadata' in key.lower()
            for repeat in scientific['repeats'] for key in repeat
        ):
            raise AssertionError('diagnostic fields leaked into scientific repeats')


if __name__ == '__main__':
    if len(sys.argv) not in (3, 4):
        raise SystemExit('usage: check_probe_v2_producers.py ACTOR_EXPORT JUDGE_EXPORT [METADATA_ACTOR_EXPORT]')
    check_actor(Path(sys.argv[1]))
    check_judge(Path(sys.argv[2]))
    if len(sys.argv) == 4:
        check_metadata_actor(Path(sys.argv[3]))
        check_actor(Path(sys.argv[3]))
    print('{"probe_v2_actual_producer_transport_valid":true}')

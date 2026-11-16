"""Strict private native-monitoring index and JSONL validation."""
import json
import re
import time

MAX_INDEX = 1048576
MAX_REPORT = 1048576
MAX_RAW = 134217728
MAX_REFS = 4096
MAX_CLIENTS = 32
MAX_DIAGNOSTICS = 128


def _pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('duplicate key')
        result[key] = value
    return result


def _decode(data):
    return json.loads(data, object_pairs_hook=_pairs,
                      parse_constant=lambda _: (_ for _ in ()).throw(ValueError('number')))


def index(data, identity, prefix):
    if len(data) > MAX_INDEX:
        raise ValueError('index size')
    document = _decode(data)
    if (not isinstance(document, dict) or set(document) != {'schema_version', 'identity', 'clients', 'capture_complete', 'diagnostics_truncated'}
            or type(document['schema_version']) is not int or document['schema_version'] != 1
            or document['identity'] != identity or type(document['capture_complete']) is not bool
            or type(document['diagnostics_truncated']) is not bool or not isinstance(document['clients'], list)
            or len(document['clients']) > MAX_CLIENTS):
        raise ValueError('index shape or identity')
    refs = diagnostics = raw_bytes = 0
    seen = set()
    for ordinal, client in enumerate(document['clients'], 1):
        if (not isinstance(client, dict) or set(client) != {'ordinal', 'role', 'client_id', 'native_identity', 'build_status', 'report_count', 'raw_sources', 'diagnostics', 'capture_complete', 'export_tail_status'}
                or type(client['ordinal']) is not int or client['ordinal'] != ordinal
                or client['role'] not in ('LoadGenProducer', 'EnrichmentProducer', 'EnrichmentConsumer', 'VerifierConsumer')
                or not isinstance(client['client_id'], str) or not client['client_id'] or len(client['client_id'].encode()) > 1024
                or (client['native_identity'] is not None and (not isinstance(client['native_identity'], str) or len(client['native_identity'].encode()) > 1024))
                or client['build_status'] not in ('built', 'failed') or type(client['report_count']) is not int
                or not 0 <= client['report_count'] <= MAX_REFS or not isinstance(client['raw_sources'], list)
                or not isinstance(client['diagnostics'], list) or type(client['capture_complete']) is not bool
                or client['export_tail_status'] not in ('unknown', 'complete', 'incomplete')):
            raise ValueError('client shape')
        if any(not isinstance(code, str) or not re.fullmatch(r'[a-z][a-z0-9_]{0,127}', code) for code in client['diagnostics']) or len(set(client['diagnostics'])) != len(client['diagnostics']):
            raise ValueError('diagnostics')
        diagnostics += len(client['diagnostics'])
        rows = 0
        existing_seen = set()
        for source in client['raw_sources']:
            refs += 1
            if (not isinstance(source, dict) or set(source) != {'kind', 'source', 'byte_length', 'sha256', 'report_ordinal', 'row_count'}
                    or source['kind'] not in ('existing_capture', 'raw_chunk') or not isinstance(source['source'], str)
                    or len(source['source']) > 1024 or any(not re.fullmatch(r'[A-Za-z0-9_.-]+', part) or part in ('.', '..') for part in source['source'].split('/'))
                    or type(source['byte_length']) is not int or source['byte_length'] < 0
                    or not isinstance(source['sha256'], str) or not re.fullmatch(r'[0-9a-f]{64}', source['sha256'])
                    or type(source['report_ordinal']) is not int or source['report_ordinal'] < 1
                    or type(source['row_count']) is not int or source['row_count'] < 1):
                raise ValueError('source shape')
            if source['kind'] == 'raw_chunk':
                if (source['source'] != f'{prefix}raw-{ordinal:06d}.jsonl' or source['source'] in seen
                        or source['report_ordinal'] + source['row_count'] - 1 > client['report_count']):
                    raise ValueError('chunk identity')
                seen.add(source['source']); raw_bytes += source['byte_length']
            elif source['row_count'] != 1 or source['byte_length'] > MAX_REPORT or (source['source'], source['report_ordinal']) in existing_seen:
                raise ValueError('existing source bounds')
            else:
                existing_seen.add((source['source'], source['report_ordinal']))
            rows += source['row_count']
        if client['capture_complete'] and (client['build_status'] != 'built' or client['diagnostics'] or rows != client['report_count']):
            raise ValueError('complete client')
    if refs > MAX_REFS or diagnostics > MAX_DIAGNOSTICS or raw_bytes > MAX_RAW:
        raise ValueError('index bounds')
    if document['capture_complete'] and (document['diagnostics_truncated'] or any(not c['capture_complete'] for c in document['clients'])):
        raise ValueError('complete index')
    return document


def chunk(path, descriptor, client_ordinal, deadline=None):
    count = 0
    with open(path, 'rb') as stream:
        while True:
            if deadline is not None and time.monotonic() >= deadline:
                raise TimeoutError('archive deadline')
            line = stream.readline(6 * MAX_REPORT + 65537)
            if not line:
                break
            if not line.endswith(b'\n') or len(line) >= 6 * MAX_REPORT + 65537:
                raise ValueError('chunk row size')
            row = _decode(line)
            if (not isinstance(row, dict) or set(row) != {'client_ordinal', 'report_ordinal', 'raw'}
                    or type(row['client_ordinal']) is not int or row['client_ordinal'] != client_ordinal
                    or type(row['report_ordinal']) is not int or row['report_ordinal'] != descriptor['report_ordinal'] + count
                    or not isinstance(row['raw'], str) or len(row['raw'].encode('utf-8')) > MAX_REPORT):
                raise ValueError('chunk row')
            count += 1
            if count > descriptor['row_count']:
                raise ValueError('chunk count')
    if count != descriptor['row_count']:
        raise ValueError('chunk count')

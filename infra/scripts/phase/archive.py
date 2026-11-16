"""Configuration and bounded transfer policy for phase evidence archives."""
import re
from urllib.parse import urlsplit

ArchiveMaxBytes = 17179869184
ArchiveMaxObjectBytes = 268435456
ArchiveMaxObjects = 1024
ArchiveDeadlineSeconds = 600
ArchiveTransferPasses = 1
MetadataChunkMaxBytes = 134217728
MetadataRowMaxBytes = 1048576


class InvalidArchiveConfig(ValueError):
    pass


def validate_archive_config(value):
    """Validate and copy the persistent Terraform archive output before side effects."""
    fields = {'storage_account_id', 'account_name', 'blob_service_uri',
              'container_name', 'container_id', 'retention_days'}
    if not isinstance(value, dict) or set(value) != fields:
        raise InvalidArchiveConfig()
    account = value['account_name']
    if not isinstance(account, str) or not re.fullmatch(r'[a-z0-9]{3,24}', account):
        raise InvalidArchiveConfig()
    url = value['blob_service_uri']
    if not isinstance(url, str):
        raise InvalidArchiveConfig()
    try:
        parsed = urlsplit(url)
        if (parsed.scheme != 'https' or parsed.netloc != account + '.blob.core.windows.net'
                or parsed.path not in ('', '/') or parsed.query or parsed.fragment
                or parsed.username is not None or parsed.password is not None or parsed.port is not None):
            raise InvalidArchiveConfig()
    except ValueError:
        raise InvalidArchiveConfig() from None
    rid = value['storage_account_id']
    if (not isinstance(rid, str) or not re.fullmatch(
            r'/subscriptions/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/resourceGroups/[A-Za-z0-9_.()\-]+/providers/Microsoft\.Storage/storageAccounts/[a-z0-9]+', rid)
            or not rid.endswith('/' + account)
            or value['container_name'] != 'run-evidence'
            or value['container_id'] != rid + '/blobServices/default/containers/run-evidence'
            or type(value['retention_days']) is not int or value['retention_days'] != 90):
        raise InvalidArchiveConfig()
    return dict(value)

# Transport runs through the same serialized CLI wrapper and bounded subprocess
# machinery as the archive readiness smoke. It never uses a shell or retries a
# failed helper invocation.
import importlib.util
import os
from pathlib import Path
import shutil
import stat
import time

_spec = importlib.util.spec_from_file_location('parchwell_archive_smoke_common',
                                               Path(__file__).resolve().parents[1] / 'archive' / 'smoke.py')
_common = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_common)
Gap = _common.Gap


class Transfer:
    def __init__(self, source_account, archive_account, spool, deadline, command=None):
        self.source_account = source_account
        self.archive_account = archive_account
        self.spool = Path(spool)
        self.deadline = deadline
        self.command = command or _common.command

    def az(self, account, container, name, action, extra):
        _common.check_deadline(self.deadline, time.monotonic)
        return self.command(['az', 'storage', 'blob', action, '--account-name', account,
                             '--container-name', container, '--name', name,
                             '--auth-mode', 'login', '--only-show-errors', *extra], self.deadline)

    def properties(self, account, container, name, missing_ok=False):
        code, output, error = self.az(account, container, name, 'show', ['--output', 'json'])
        if code:
            if missing_ok and re.search(rb'\b(?:BlobNotFound|ResourceNotFound)\b', error):
                return None
            raise Gap('transport', 'properties')
        try:
            import json
            data = json.loads(output, object_pairs_hook=_common.pairs)
            size, etag = data['properties']['contentLength'], data['properties']['etag']
            if type(size) is not int or size < 0:
                code = ('source_too_large' if account == self.source_account else
                        'destination_conflict' if missing_ok else 'destination_length_mismatch')
                raise Gap(code, 'object properties')
            if size > ArchiveMaxObjectBytes:
                code = ('source_too_large' if account == self.source_account else
                        'destination_conflict' if missing_ok else 'destination_length_mismatch')
                raise Gap(code, 'object properties')
            if not isinstance(etag, str) or not etag or len(etag) > 1024:
                raise ValueError()
            return size, etag
        except (ValueError, KeyError, TypeError, _common.Invalid):
            raise Gap('transport', 'invalid properties') from None

    def download(self, account, container, name, etag, path):
        code, _, error = self.az(account, container, name, 'download',
                             ['--file', str(path), '--if-match', etag, '--overwrite', 'false',
                              '--max-connections', '1', '--output', 'none'])
        if code:
            if account == self.source_account and re.search(rb'\b(?:ConditionNotMet|ResourceModifiedError)\b', error):
                raise Gap('source_changed', 'conditional download')
            raise Gap('transport', 'conditional download')

    def upload(self, container, name, path):
        code, _, error = self.az(self.archive_account, container, name, 'upload',
                                 ['--file', str(path), '--if-none-match', '*', '--overwrite', 'false',
                                  '--max-connections', '1', '--output', 'none'])
        if code:
            if re.search(rb'\b(?:BlobAlreadyExists|ConditionNotMet)\b', error):
                raise Gap('destination_conflict', 'conditional create')
            raise Gap('transport', 'conditional create')

    def project_source(self, container, name, destination, remaining, validate, reserve=None,
                       projection_format='single_action_capture', expected_sha256=None):
        """Read one original once, project bodies away, and verify only the projection remotely."""
        import hashlib
        if not safe_name(container) or not safe_name(name) or not safe_name(destination): raise InvalidArchiveConfig()
        if shutil.disk_usage(self.spool).free < 2 * ArchiveMaxObjectBytes + 1048576 + 65536:
            raise Gap('disk_space', 'spool reserve')
        source, projected, back = (self.spool / 'source.partial', self.spool / 'projection.partial', self.spool / 'readback.partial')
        try:
            props = self.properties(self.source_account, container, name, missing_ok=True)
            if props is None: raise Gap('source_missing', 'source properties')
            size, etag = props
            if projection_format == 'probe_v2_candidate' and expected_sha256 is None:
                raise Gap('monitoring_incomplete', 'candidate receipt hash unavailable')
            if projection_format == 'probe_v2_candidate' and size > 16777216:
                raise Gap('source_too_large', 'candidate size')
            self.download(self.source_account, container, name, etag, source)
            source_hash = _common.digest(source, size, deadline=self.deadline)
            if projection_format == 'probe_v2_candidate' and source_hash != expected_sha256:
                raise Gap('source_hash_mismatch', 'candidate receipt hash')
            source_ref = {'container': container, 'name': name, 'etag': etag, 'bytes': size, 'sha256': source_hash}
            spec = importlib.util.spec_from_file_location('parchwell_archive_json', Path(__file__).with_name('archive_json.py'))
            module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
            class Bounded:
                def __init__(self, output): self.output = output; self.length = 0
                def write(self, data):
                    _common.check_deadline(self_deadline, time.monotonic)
                    if len(data) > ArchiveMaxObjectBytes - self.length: raise Gap('source_too_large', 'projection size')
                    self.output.write(data); self.length += len(data)
            self_deadline = self.deadline
            fd = os.open(projected, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
            with os.fdopen(fd, 'wb') as raw:
                out = Bounded(raw)
                header = _common.encoded({'schema_version': 1, 'kind': 'source_metadata_projection',
                                          'source': source_ref, 'format': projection_format}).rstrip(b'\n')
                out.write(header[:-1] + b',"metadata":')
                try:
                    if projection_format == 'probe_v2_candidate':
                        document = module.candidate(source, self.deadline, out, validate)
                    else:
                        document = module.project(source, self.deadline, out)
                except TimeoutError: raise Gap('deadline', 'source projection') from None
                except (module.Invalid, UnicodeError): raise Gap('monitoring_incomplete', 'source projection contract') from None
                out.write(b'}\n')
                length = out.length
            valid = True if projection_format == 'probe_v2_candidate' else validate(document)
            if length > remaining: raise Gap('byte_limit', 'phase bytes')
            if reserve is not None: reserve(length)
            projection_hash = _common.digest(projected, length, deadline=self.deadline)
            source.unlink()
            existing = self.properties(self.archive_account, 'run-evidence', destination, missing_ok=True)
            present = existing is not None
            if existing is None:
                self.upload('run-evidence', destination, projected)
                existing = self.properties(self.archive_account, 'run-evidence', destination)
            dest_size, dest_etag = existing
            if dest_size != length: raise Gap('destination_conflict' if present else 'destination_length_mismatch', 'projection properties')
            self.download(self.archive_account, 'run-evidence', destination, dest_etag, back)
            if _common.digest(back, length, deadline=self.deadline, side='destination') != projection_hash:
                raise Gap('destination_conflict' if present else 'destination_hash_mismatch', 'projection readback')
            return length, projection_hash, etag, dest_etag, source_ref, valid
        finally:
            source.unlink(missing_ok=True); projected.unlink(missing_ok=True); back.unlink(missing_ok=True)

    def copy_local(self, source_path, destination, remaining, reserve=None):
        """Snapshot an approved runner file and verify the archived snapshot."""
        import hashlib
        if not safe_name(destination) or type(remaining) is not int or remaining < 0:
            raise InvalidArchiveConfig()
        if shutil.disk_usage(self.spool).free < 2 * ArchiveMaxObjectBytes + 1048576 + 65536:
            raise Gap('disk_space', 'spool reserve')
        source, back = self.spool / 'source.partial', self.spool / 'readback.partial'
        try:
            fd = os.open(source_path, os.O_RDONLY | os.O_NOFOLLOW)
            with os.fdopen(fd, 'rb') as src:
                info = os.fstat(src.fileno())
                if not stat.S_ISREG(info.st_mode) or info.st_size > ArchiveMaxObjectBytes:
                    raise Gap('source_too_large', 'runner file')
                if info.st_size > remaining:
                    raise Gap('byte_limit', 'phase bytes')
                if reserve is not None:
                    reserve(info.st_size)
                output_fd = os.open(source, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
                with os.fdopen(output_fd, 'wb') as out:
                    digest = hashlib.sha256()
                    total = 0
                    while True:
                        _common.check_deadline(self.deadline, time.monotonic)
                        chunk = src.read(min(65536, info.st_size - total + 1))
                        if not chunk: break
                        total += len(chunk)
                        if total > info.st_size: raise Gap('source_length_mismatch', 'runner file')
                        digest.update(chunk); out.write(chunk)
                if total != info.st_size:
                    raise Gap('source_length_mismatch', 'runner file')
            existing = self.properties(self.archive_account, 'run-evidence', destination, missing_ok=True)
            already_present = existing is not None
            if existing is None:
                self.upload('run-evidence', destination, source)
                existing = self.properties(self.archive_account, 'run-evidence', destination)
            size, etag = existing
            if size != total:
                raise Gap('destination_conflict' if already_present else 'destination_length_mismatch', 'runner properties')
            self.download(self.archive_account, 'run-evidence', destination, etag, back)
            if _common.digest(back, size, deadline=self.deadline, side='destination') != digest.hexdigest():
                raise Gap('destination_conflict' if already_present else 'destination_hash_mismatch', 'runner readback')
            return total, digest.hexdigest(), etag
        finally:
            source.unlink(missing_ok=True)
            back.unlink(missing_ok=True)

    def write_control(self, destination, content, cap):
        """Create or verify a control object and return its exact reference."""
        import hashlib
        if not safe_name(destination) or not isinstance(content, bytes) or cap not in (1048576, 65536):
            raise InvalidArchiveConfig()
        if len(content) > cap:
            raise Gap('manifest_too_large' if cap == 1048576 else 'byte_limit', 'control size')
        if shutil.disk_usage(self.spool).free < 2 * ArchiveMaxObjectBytes + 1048576 + 65536:
            raise Gap('disk_space', 'spool reserve')
        source, back = self.spool / 'control.partial', self.spool / 'readback.partial'
        expected = hashlib.sha256(content).hexdigest()
        try:
            _common.safe_write(source, content)
            existing = self.properties(self.archive_account, 'run-evidence', destination, missing_ok=True)
            already_present = existing is not None
            if existing is None:
                self.upload('run-evidence', destination, source)
                existing = self.properties(self.archive_account, 'run-evidence', destination)
            size, etag = existing
            if size != len(content):
                raise Gap('destination_conflict' if already_present else 'destination_length_mismatch', 'control properties')
            self.download(self.archive_account, 'run-evidence', destination, etag, back)
            if _common.digest(back, size, cap, deadline=self.deadline, side='destination') != expected:
                raise Gap('destination_conflict' if already_present else 'destination_hash_mismatch', 'control readback')
            return {'name': destination, 'bytes': size, 'sha256': expected}
        finally:
            source.unlink(missing_ok=True)
            back.unlink(missing_ok=True)

    def copy(self, container, name, destination, remaining, max_object=ArchiveMaxObjectBytes, expected_sha256=None, expected_length=None, inspect_source=None, reserve=None, source_properties=None):
        """Copy and verify one source with exactly one pass and bounded local files."""
        if not safe_name(container) or '/' in container or not safe_name(name) or not safe_name(destination):
            raise InvalidArchiveConfig()
        if type(remaining) is not int or remaining < 0 or type(max_object) is not int or not 0 <= max_object <= ArchiveMaxObjectBytes:
            raise InvalidArchiveConfig()
        if expected_length is not None and (type(expected_length) is not int or not 0 <= expected_length <= max_object):
            raise InvalidArchiveConfig()
        if expected_sha256 is not None and (not isinstance(expected_sha256, str) or not re.fullmatch(r'[0-9a-f]{64}', expected_sha256)):
            raise InvalidArchiveConfig()
        if shutil.disk_usage(self.spool).free < 2 * ArchiveMaxObjectBytes + 1048576 + 65536:
            raise Gap('disk_space', 'spool reserve')
        source, back = self.spool / 'source.partial', self.spool / 'readback.partial'
        try:
            properties = source_properties if source_properties is not None else self.properties(self.source_account, container, name, missing_ok=True)
            if properties is None:
                raise Gap('source_missing', 'source properties')
            size, etag = properties
            source_length_mismatch = expected_length is not None and size != expected_length
            if size > max_object:
                raise Gap('source_too_large', 'object size')
            if size > remaining:
                raise Gap('byte_limit', 'phase bytes')
            if reserve is not None:
                reserve(size)
            self.download(self.source_account, container, name, etag, source)
            sha = _common.digest(source, size, max_object, deadline=self.deadline)
            source_hash_mismatch = expected_sha256 is not None and sha != expected_sha256
            if inspect_source is not None:
                inspect_source(source, size)
            existing = self.properties(self.archive_account, 'run-evidence', destination, missing_ok=True)
            already_present = existing is not None
            if existing is None:
                self.upload('run-evidence', destination, source)
                existing = self.properties(self.archive_account, 'run-evidence', destination)
            dest_size, dest_etag = existing
            if dest_size != size:
                raise Gap('destination_conflict' if already_present else 'destination_length_mismatch', 'destination properties')
            self.download(self.archive_account, 'run-evidence', destination, dest_etag, back)
            if _common.digest(back, size, max_object, deadline=self.deadline, side='destination') != sha:
                raise Gap('destination_conflict' if already_present else 'destination_hash_mismatch', 'destination readback')
            if source_length_mismatch or source_hash_mismatch:
                error = Gap('source_length_mismatch' if source_length_mismatch else 'source_hash_mismatch',
                            'source descriptor' if source_length_mismatch else 'source digest')
                error.retained = (size, sha, etag, dest_etag)
                raise error
            return size, sha, etag, dest_etag
        finally:
            source.unlink(missing_ok=True)
            back.unlink(missing_ok=True)

GAP_CODES = frozenset(('config_invalid', 'source_missing', 'capture_missing', 'capture_incomplete',
                       'source_changed', 'source_too_large', 'source_length_mismatch', 'source_hash_mismatch',
                       'destination_conflict', 'destination_length_mismatch', 'destination_hash_mismatch',
                       'manifest_too_large', 'object_limit', 'byte_limit', 'deadline', 'disk_space',
                       'transport', 'interrupted', 'monitoring_incomplete', 'diagnostics_truncated'))


def safe_name(value):
    return (isinstance(value, str) and 0 < len(value) <= 1024 and value.isascii()
            and all(part not in ('', '.', '..') and re.fullmatch(r'[A-Za-z0-9_.-]+', part)
                    for part in value.split('/')))


def validate_identity(identity):
    if not isinstance(identity, dict) or set(identity) != {'github_run_id', 'github_run_attempt', 'run_id', 'phase', 'source_commit'}:
        raise InvalidArchiveConfig()
    if (not isinstance(identity['github_run_id'], str) or not re.fullmatch(r'[1-9][0-9]*', identity['github_run_id'])
            or len(identity['github_run_id']) > 32 or type(identity['github_run_attempt']) is not int or not 0 < identity['github_run_attempt'] < 1000000000
            or not isinstance(identity['run_id'], str) or len(identity['run_id']) > 29
            or not re.fullmatch(r'[a-z0-9](?:[a-z0-9-]*[a-z0-9])?', identity['run_id'])
            or not isinstance(identity['phase'], str) or len(identity['phase']) > 1024
            or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9-]*', identity['phase'])
            or not isinstance(identity['source_commit'], str) or not re.fullmatch(r'[0-9a-f]{40}', identity['source_commit'])):
        raise InvalidArchiveConfig()


def validate_reference(reference, limit):
    if (not isinstance(reference, dict) or set(reference) != {'name', 'bytes', 'sha256'}
            or not safe_name(reference['name']) or type(reference['bytes']) is not int
            or not 0 <= reference['bytes'] <= limit or not isinstance(reference['sha256'], str)
            or not re.fullmatch(r'[0-9a-f]{64}', reference['sha256'])):
        raise InvalidArchiveConfig()


def validate_gaps(gaps):
    if not isinstance(gaps, list) or len(gaps) > 100:
        raise InvalidArchiveConfig()
    for gap in gaps:
        if (not isinstance(gap, dict) or set(gap) != {'code', 'boundary', 'object_name', 'expected_unit'}
                or not isinstance(gap['code'], str) or gap['code'] not in GAP_CODES or not isinstance(gap['boundary'], str)
                or len(gap['boundary'].encode('utf-8')) > 2048
                or any(value is not None and (not isinstance(value, str) or len(value) > 1024 or not value.isascii())
                       for value in (gap['object_name'], gap['expected_unit']))):
            raise InvalidArchiveConfig()


def validate_receipt(receipt):
    if (not isinstance(receipt, dict) or set(receipt) != {'schema_version', 'kind', 'identity', 'archive_complete', 'manifest', 'completion', 'gaps'}
            or type(receipt['schema_version']) is not int or receipt['schema_version'] != 1
            or receipt['kind'] != 'run_evidence_archive_receipt' or type(receipt['archive_complete']) is not bool):
        raise InvalidArchiveConfig()
    validate_identity(receipt['identity'])
    validate_gaps(receipt['gaps'])
    if receipt['manifest'] is not None:
        validate_reference(receipt['manifest'], 1048576)
    if receipt['completion'] is not None:
        validate_reference(receipt['completion'], 65536)
    if receipt['archive_complete']:
        if receipt['gaps'] or receipt['manifest'] is None or receipt['completion'] is None:
            raise InvalidArchiveConfig()
    elif not receipt['gaps'] or receipt['completion'] is not None:
        raise InvalidArchiveConfig()


def ordinary_fixed_inventory(run, phase, modes):
    """Derive fixed verifier objects from recorded ordinary job registrations.

    Producer and capture objects require their observed pod/registration identities;
    those must be added by the caller, not invented from a directory listing.
    """
    if not safe_name(run) or '/' in run or not safe_name(phase) or '/' in phase or not isinstance(modes, list):
        raise InvalidArchiveConfig()
    inventory = []
    fixed = {'start-line': ('start-line.json', 'broker-inventory.json', 'start-line-completion.json'),
             'verify': ('result.json', 'verify-completion.json'),
             'judge': ('candidate.json', 'judge-completion.json'),
             'b10-sweep-line': ('sweep-line.json', 'b10-sweep-line-completion.json'),
             'b10-sweep': ('b10-finalization.json', 'b10-sweep-completion.json'),
             'b10-final-sweep': ('b10-final-sweep.json', 'b10-final-sweep-completion.json')}
    for record in modes:
        if not isinstance(record, dict) or record.get('mode') not in fixed:
            continue
        mode, repeat = record['mode'], record.get('repeat')
        if mode in ('start-line', 'verify'):
            if type(repeat) is not int or repeat < 1:
                raise InvalidArchiveConfig()
            prefix = f'{run}/{phase}/r{repeat}/'
        else:
            prefix = f'{run}/{phase}/'
        started = record.get('apply') is not None
        for name in fixed[mode]:
            inventory.append({'container': 'verifier', 'name': prefix + name, 'repeat': repeat,
                              'required': started, 'state': 'pending' if started else 'not_started'})
    return sorted(inventory, key=lambda item: (item['container'], item['name']))


def strict_control(data, maximum):
    import json
    if not isinstance(data, bytes) or len(data) > maximum:
        raise InvalidArchiveConfig()
    try:
        result = json.loads(data, object_pairs_hook=_common.pairs,
                            parse_constant=lambda _: (_ for _ in ()).throw(InvalidArchiveConfig()))
        def depth(value, level):
            if level > 32:
                raise InvalidArchiveConfig()
            if isinstance(value, dict):
                for item in value.values(): depth(item, level + 1)
            elif isinstance(value, list):
                for item in value: depth(item, level + 1)
        depth(result, 1)
        return result
    except (ValueError, UnicodeError, RecursionError, _common.Invalid):
        raise InvalidArchiveConfig() from None


def validate_totals(totals):
    if (not isinstance(totals, dict) or set(totals) != {'expected_objects', 'verified_objects', 'verified_bytes'}
            or any(type(value) is not int or value < 0 for value in totals.values())
            or totals['expected_objects'] > ArchiveMaxObjects - 2
            or totals['verified_objects'] > totals['expected_objects']
            or totals['verified_bytes'] > ArchiveMaxBytes):
        raise InvalidArchiveConfig()


def validate_marker(marker):
    if (not isinstance(marker, dict) or set(marker) != {'schema_version', 'kind', 'identity', 'manifest', 'totals', 'archive_complete'}
            or type(marker['schema_version']) is not int or marker['schema_version'] != 1
            or marker['kind'] != 'run_evidence_archive_completion' or marker['archive_complete'] is not True):
        raise InvalidArchiveConfig()
    validate_identity(marker['identity'])
    validate_reference(marker['manifest'], 1048576)
    validate_totals(marker['totals'])
    if marker['totals']['expected_objects'] != marker['totals']['verified_objects']:
        raise InvalidArchiveConfig()




def capture_inventory(data, prefix, identity, mode, repeat, capture_id):
    """Validate a capture publication and derive the immutable chunk references."""
    capture = strict_control(data, 1048576)
    fields = {'schema_version', 'capture_id', 'identity', 'window', 'chunks', 'observed_rows',
              'captured_rows', 'complete', 'first_missing_ordinal', 'gaps'}
    if (not isinstance(capture, dict) or set(capture) != fields or type(capture['schema_version']) is not int
            or capture['schema_version'] != 1 or capture['capture_id'] != capture_id
            or capture['identity'] != identity or type(capture['complete']) is not bool
            or not isinstance(capture['gaps'], list) or len(capture['gaps']) > 100
            or type(capture['observed_rows']) is not int or capture['observed_rows'] < 0
            or type(capture['captured_rows']) is not int or capture['captured_rows'] < 0
            or capture['captured_rows'] > capture['observed_rows'] or not isinstance(capture['chunks'], list)
            or len(capture['chunks']) > ArchiveMaxObjects - 2 or not safe_name(prefix.rstrip('/'))):
        raise InvalidArchiveConfig()
    first = capture['first_missing_ordinal']
    if first is not None and (type(first) is not int or first < 0 or first > capture['observed_rows']):
        raise InvalidArchiveConfig()
    identity_fields = {'run_id', 'phase', 'scenario_id', 'configuration_id', 'job_name', 'pod_uid', 'source_commit'}
    if not isinstance(identity, dict) or set(identity) != identity_fields or any(not isinstance(x, str) for x in identity.values()):
        raise InvalidArchiveConfig()
    capture_codes = {'row_limit', 'byte_limit', 'write_failed', 'upload_failed', 'interrupted', 'source_window_incomplete', 'diagnostics_truncated'}
    for gap in capture['gaps']:
        if (not isinstance(gap, dict) or set(gap) != {'code', 'ordinal'} or not isinstance(gap['code'], str)
                or gap['code'] not in capture_codes or (gap['ordinal'] is not None and (type(gap['ordinal']) is not int or gap['ordinal'] < 0))):
            raise InvalidArchiveConfig()
    if capture['complete'] and (capture['gaps'] or first is not None or capture['observed_rows'] != capture['captured_rows']):
        raise InvalidArchiveConfig()
    rows = 0
    result = []
    for index, chunk in enumerate(capture['chunks'], 1):
        if (not isinstance(chunk, dict) or set(chunk) != {'name', 'bytes', 'sha256', 'rows'}
                or chunk['name'] != f'{prefix}chunk-{index:06d}.jsonl'
                or type(chunk['bytes']) is not int or not 0 <= chunk['bytes'] <= MetadataChunkMaxBytes
                or type(chunk['rows']) is not int or chunk['rows'] <= 0
                or not isinstance(chunk['sha256'], str) or not re.fullmatch(r'[0-9a-f]{64}', chunk['sha256'])):
            raise InvalidArchiveConfig()
        rows += chunk['rows']
        result.append(dict(chunk))
    if rows != capture['captured_rows']:
        raise InvalidArchiveConfig()
    window = capture['window']
    if (not isinstance(window, dict) or set(window) != {'index', 'kind', 'repeat', 'topic', 'start', 'end'}
            or type(window['index']) is not int or window['index'] < 0
            or (window['repeat'] is not None and (type(window['repeat']) is not int or window['repeat'] < 1))
            or window['kind'] not in ('ordinary', 'original', 'late', 'final') or not isinstance(window['topic'], str)
            or not isinstance(window['start'], list) or not isinstance(window['end'], list)):
        raise InvalidArchiveConfig()
    if mode in ('verify', 'b10-abrupt-verify'):
        expected_index, expected_kind = 0, 'ordinary'
    elif mode == 'b10-final-sweep':
        expected_index, expected_kind = 0, 'final'
    elif mode == 'b10-sweep' and type(repeat) is int and 1 <= repeat <= 5:
        if window['index'] not in (2 * (repeat - 1), 2 * (repeat - 1) + 1):
            raise InvalidArchiveConfig()
        expected_index = window['index']
        expected_kind = 'original' if expected_index % 2 == 0 else 'late'
    else:
        raise InvalidArchiveConfig()
    if (window['topic'] != 'document-events' or window['index'] != expected_index
            or window['kind'] != expected_kind or not capture_id.endswith(f"-w{expected_index:04d}")
            or window['repeat'] != (None if expected_kind == 'final' else repeat)):
        raise InvalidArchiveConfig()
    for vector in (window['start'], window['end']):
        partitions = []
        for entry in vector:
            if (not isinstance(entry, dict) or set(entry) != {'partition', 'offset'}
                    or type(entry['partition']) is not int or entry['partition'] < 0
                    or type(entry['offset']) is not int or entry['offset'] < 0):
                raise InvalidArchiveConfig()
            partitions.append(entry['partition'])
        if partitions != sorted(set(partitions)): raise InvalidArchiveConfig()
    starts = {entry['partition']: entry['offset'] for entry in window['start']}
    ends = {entry['partition']: entry['offset'] for entry in window['end']}
    if capture['complete'] and (not starts or starts.keys() != ends.keys()
                                or any(starts[partition] > ends[partition] for partition in starts)):
        raise InvalidArchiveConfig()
    return {'capture_id': capture_id, 'mode': mode, 'repeat': repeat, 'required': True,
            'complete': capture['complete'], 'rows': capture['captured_rows'], 'first_missing_ordinal': first}, result


def write_receipt(public, receipt):
    target = Path(public) / 'archive-receipt.json'
    partial = Path(public) / 'archive-receipt.json.partial'
    data = _common.encoded(receipt)
    if len(data) > 65536:
        if receipt['archive_complete']:
            raise Gap('byte_limit', 'receipt size')
        truncated = {'code': 'diagnostics_truncated', 'boundary': 'receipt size', 'object_name': None, 'expected_unit': None}
        kept = [gap for gap in receipt['gaps'] if gap['code'] != 'diagnostics_truncated']
        while True:
            receipt['gaps'] = sorted(kept + [truncated], key=lambda gap: (gap['code'], gap['boundary'], gap['object_name'] or '', gap['expected_unit'] or ''))
            data = _common.encoded(receipt)
            if len(data) <= 65536: break
            if not kept: raise Gap('byte_limit', 'receipt size')
            kept.pop()
    try:
        _common.safe_write(partial, data)
        os.replace(partial, target)
    finally:
        partial.unlink(missing_ok=True)


def ensure_canonical(path, content):
    try:
        _common.safe_write(path, content)
    except FileExistsError:
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
        with os.fdopen(fd, 'rb') as stream:
            if not stat.S_ISREG(os.fstat(stream.fileno()).st_mode) or stream.read(1048577) != content:
                raise Gap('destination_conflict', 'local canonical mismatch')


def publish_controls(transfer, manifest, identity, public, prefix):
    """Publish canonical bytes and then the marker; marker failure never rewrites them."""
    if validate_manifest(manifest) != identity:
        raise InvalidArchiveConfig()
    if not safe_name(prefix.rstrip('/')) or not prefix.endswith('/') or manifest['destination']['prefix'] != prefix:
        raise InvalidArchiveConfig()
    if manifest.get('completion', object()) is not None or type(manifest.get('archive_complete')) is not bool:
        raise InvalidArchiveConfig()
    gaps = list(manifest.get('gaps', []))
    validate_gaps(gaps)
    content = _common.encoded(manifest)
    manifest_ref = None
    completion_ref = None
    public = Path(public)
    if len(content) > 1048576:
        raise Gap('manifest_too_large', 'canonical manifest')
    try:
        ensure_canonical(public / 'archive-manifest.json', content)
        manifest_ref = transfer.write_control(prefix + 'archive-manifest.json', content, 1048576)
        if manifest['archive_complete'] and not gaps:
            marker = {'schema_version': 1, 'kind': 'run_evidence_archive_completion', 'identity': identity,
                      'manifest': manifest_ref, 'totals': manifest['totals'], 'archive_complete': True}
            validate_marker(marker)
            completion_ref = transfer.write_control(prefix + 'archive-completion.json', _common.encoded(marker), 65536)
            _common.check_deadline(transfer.deadline, time.monotonic)
    except Gap as error:
        gaps.append({'code': error.code, 'boundary': error.boundary, 'object_name': None, 'expected_unit': None})
    except Exception:
        gaps.append({'code': 'transport', 'boundary': 'control publication', 'object_name': None, 'expected_unit': None})
    except KeyboardInterrupt:
        gaps.append({'code': 'interrupted', 'boundary': 'control publication', 'object_name': None, 'expected_unit': None})
    if len(gaps) > 100:
        gaps = gaps[:99] + [{'code': 'diagnostics_truncated', 'boundary': 'diagnostic count', 'object_name': None, 'expected_unit': None}]
    gaps.sort(key=lambda gap: (gap['code'], gap['boundary'], gap['object_name'] or '', gap['expected_unit'] or ''))
    success = completion_ref is not None and not gaps
    receipt = {'schema_version': 1, 'kind': 'run_evidence_archive_receipt', 'identity': identity,
               'archive_complete': success, 'manifest': manifest_ref, 'completion': completion_ref if success else None,
               'gaps': gaps}
    validate_receipt(receipt)
    write_receipt(public, receipt)
    return receipt


def validate_manifest(value):
    fields = {'schema_version', 'kind', 'archive_complete', 'run', 'destination', 'policy', 'expected_units',
              'objects', 'captures', 'totals', 'gaps', 'completion'}
    if (not isinstance(value, dict) or set(value) != fields or type(value['schema_version']) is not int
            or value['schema_version'] != 1 or value['kind'] != 'run_evidence_archive'
            or type(value['archive_complete']) is not bool or value['completion'] is not None):
        raise InvalidArchiveConfig()
    run = value['run']
    run_fields = {'github_run_id', 'github_run_attempt', 'run_id', 'scenario_id', 'configuration_id', 'phase',
                  'source_commit', 'images', 'run_kind', 'purpose', 'scientific_admissible'}
    if (not isinstance(run, dict) or set(run) != run_fields or type(run['scientific_admissible']) is not bool
            or any(not isinstance(run[key], str) or not run[key].isascii() or len(run[key]) > 1024
                   for key in ('scenario_id', 'configuration_id', 'run_kind', 'purpose'))
            or not isinstance(run['images'], dict) or set(run['images']) != {'loadgen', 'verifier', 'probe'}
            or any(not run[key] for key in ('scenario_id', 'configuration_id', 'run_kind', 'purpose'))
            or any(reference is not None and (not isinstance(reference, str) or len(reference) > 1024
                   or not re.fullmatch(r'[a-z0-9.-]+/parchwell/' + worker + r'@sha256:[0-9a-f]{64}', reference))
                   for worker, reference in run['images'].items())):
        raise InvalidArchiveConfig()
    identity = {key: run[key] for key in ('github_run_id', 'github_run_attempt', 'run_id', 'phase', 'source_commit')}
    validate_identity(identity)
    destination = value['destination']
    prefix = 'v1/{}/{}/{}/{}/{}/'.format(identity['source_commit'], identity['github_run_id'], identity['github_run_attempt'], identity['run_id'], identity['phase'])
    if (not isinstance(destination, dict) or set(destination) != {'account_host', 'container', 'prefix', 'retention_days'}
            or not isinstance(destination['account_host'], str)
            or not re.fullmatch(r'[a-z0-9]{3,24}\.blob\.core\.windows\.net', destination['account_host'])
            or destination['container'] != 'run-evidence' or destination['prefix'] != prefix
            or type(destination['retention_days']) is not int or destination['retention_days'] != 90
            or not isinstance(value['policy'], dict) or value['policy'] != _common.POLICY
            or any(type(x) is not int for x in value['policy'].values())):
        raise InvalidArchiveConfig()
    validate_gaps(value['gaps']); validate_totals(value['totals'])
    if not isinstance(value['objects'], list) or len(value['objects']) > ArchiveMaxObjects - 2:
        raise InvalidArchiveConfig()
    expected = verified = size = 0
    names = set()
    for obj in value['objects']:
        keys = {'kind', 'source', 'destination_name', 'required', 'repeat', 'capture_id', 'bytes', 'sha256', 'destination_etag', 'state', 'gap_code'}
        if (not isinstance(obj, dict) or set(obj) != keys or not isinstance(obj['kind'], str)
                or type(obj['required']) is not bool or obj['state'] not in ('verified', 'missing', 'rejected', 'failed', 'not_started')
                or not safe_name(obj['destination_name']) or not obj['destination_name'].startswith(prefix)
                or obj['destination_name'] in names or not isinstance(obj['source'], dict)
                or set(obj['source']) != {'container', 'name', 'etag'} or not safe_name(obj['source']['container'])
                or not safe_name(obj['source']['name']) or obj['source']['container'] not in ('ledger', 'verifier', 'single-action', 'native-monitoring', 'runner')
                or obj['destination_name'] != ((prefix + 'runner/' + obj['source']['name'] if obj['source']['container'] == 'runner' else prefix + 'objects/' + obj['source']['container'] + '/' + obj['source']['name'])
                                                + ('.metadata.json' if obj['kind'] == 'source_metadata_projection' else ''))
                or (obj['repeat'] is not None and (type(obj['repeat']) is not int or obj['repeat'] < 1))
                or (obj['capture_id'] is not None and not safe_name(obj['capture_id']))):
            raise InvalidArchiveConfig()
        names.add(obj['destination_name']); expected += int(obj['required'])
        if obj['state'] == 'verified':
            if (type(obj['bytes']) is not int or not 0 <= obj['bytes'] <= ArchiveMaxObjectBytes
                    or not isinstance(obj['sha256'], str) or not re.fullmatch(r'[0-9a-f]{64}', obj['sha256'])
                    or (obj['source']['container'] != 'runner' and (not isinstance(obj['source']['etag'], str) or not obj['source']['etag']))
                    or (obj['source']['container'] == 'runner' and obj['source']['etag'] is not None)
                    or not isinstance(obj['destination_etag'], str) or not obj['destination_etag'] or obj['gap_code'] is not None):
                raise InvalidArchiveConfig()
            verified += 1; size += obj['bytes']
        elif obj['state'] == 'not_started':
            if obj['required'] or any(obj[key] is not None for key in ('bytes', 'sha256', 'destination_etag', 'gap_code')):
                raise InvalidArchiveConfig()
        elif not isinstance(obj['gap_code'], str) or obj['gap_code'] not in GAP_CODES:
            raise InvalidArchiveConfig()
        elif any(obj[key] is not None for key in ('bytes', 'sha256', 'destination_etag')):
            if (obj['state'] not in ('failed', 'rejected') or obj['gap_code'] not in ('source_length_mismatch', 'source_hash_mismatch', 'monitoring_incomplete', 'capture_incomplete')
                    or type(obj['bytes']) is not int or not 0 <= obj['bytes'] <= ArchiveMaxObjectBytes
                    or not isinstance(obj['sha256'], str) or not re.fullmatch(r'[0-9a-f]{64}', obj['sha256'])
                    or not isinstance(obj['source']['etag'], str) or not obj['source']['etag']
                    or not isinstance(obj['destination_etag'], str) or not obj['destination_etag']):
                raise InvalidArchiveConfig()
    if value['totals'] != {'expected_objects': expected, 'verified_objects': verified, 'verified_bytes': size}:
        raise InvalidArchiveConfig()
    if not isinstance(value['captures'], list) or not isinstance(value['expected_units'], list):
        raise InvalidArchiveConfig()
    for capture in value['captures']:
        if (not isinstance(capture, dict) or set(capture) != {'capture_id', 'mode', 'repeat', 'required', 'complete', 'rows', 'first_missing_ordinal'}
                or not safe_name(capture['capture_id']) or not isinstance(capture['mode'], str)
                or type(capture['required']) is not bool or type(capture['complete']) is not bool
                or (capture['repeat'] is not None and (type(capture['repeat']) is not int or capture['repeat'] < 1))
                or any(x is not None and (type(x) is not int or x < 0) for x in (capture['rows'], capture['first_missing_ordinal']))
                or (capture['complete'] and (capture['rows'] is None or capture['first_missing_ordinal'] is not None))):
            raise InvalidArchiveConfig()
    for unit in value['expected_units']:
        if (not isinstance(unit, dict) or set(unit) != {'repeat', 'item', 'mode', 'job_name', 'pod_name', 'pod_uid', 'instance_id', 'state'}
                or unit['state'] not in ('started', 'not_started') or not safe_name(unit['job_name'])
                or not isinstance(unit['mode'], str)
                or (unit['repeat'] is not None and (type(unit['repeat']) is not int or unit['repeat'] < 1))
                or any(x is not None and not safe_name(x) for x in (unit['item'], unit['pod_name'], unit['pod_uid'], unit['instance_id']))):
            raise InvalidArchiveConfig()
    if (value['objects'] != sorted(value['objects'], key=lambda obj: obj['destination_name'])
            or value['captures'] != sorted(value['captures'], key=lambda capture: capture['capture_id'])
            or value['expected_units'] != sorted(value['expected_units'], key=lambda unit: (unit['job_name'], unit['repeat'] or 0, unit['pod_name'] or '', unit['pod_uid'] or ''))
            or value['gaps'] != sorted(value['gaps'], key=lambda gap: (gap['code'], gap['boundary'], gap['object_name'] or '', gap['expected_unit'] or ''))):
        raise InvalidArchiveConfig()
    if value['archive_complete'] and (value['gaps'] or expected != verified
            or any(capture['required'] and not capture['complete'] for capture in value['captures'])):
        raise InvalidArchiveConfig()
    if not value['archive_complete'] and not value['gaps']:
        raise InvalidArchiveConfig()
    return identity


def ordinary_units(run, phase, records):
    """Derive all ordinary scheduled units, including jobs never reached."""
    if not safe_name(run) or '/' in run or not safe_name(phase) or '/' in phase or not isinstance(records, list):
        raise InvalidArchiveConfig()
    planned = [(repeat, mode) for repeat in range(1, 6) for mode in ('start-line', 'loadgen', 'verify')]
    planned.append((None, 'judge'))
    indexed = {}
    for record in records:
        if not isinstance(record, dict):
            raise InvalidArchiveConfig()
        key = (record.get('repeat'), record.get('mode'))
        if key not in planned or key in indexed:
            raise InvalidArchiveConfig()
        indexed[key] = record
    result = []
    gaps = []
    for repeat, mode in planned:
        job = f'{run}-{phase.lower()}' + (f'-r{repeat}' if repeat is not None else '') + f'-{mode}'
        record = indexed.get((repeat, mode))
        if record is not None and record.get('job') != job:
            raise InvalidArchiveConfig()
        if record is None:
            gaps.append({'code': 'monitoring_incomplete', 'boundary': 'missing job status', 'object_name': None, 'expected_unit': job})
        result.append({'repeat': repeat, 'item': None, 'mode': mode, 'job_name': job,
                       'pod_name': None, 'pod_uid': None, 'instance_id': None,
                       'state': 'started' if record is not None and record.get('apply') is not None else 'not_started'})
    return result, gaps


def load_context(environ):
    """Load only frozen phase and lifecycle outputs before creating the spool."""
    try:
        private = Path(environ['PARCHWELL_PRIVATE_DIRECTORY'])
        artifacts = Path(environ['PARCHWELL_ARTIFACT_DIRECTORY'])
        if not private.is_absolute() or not artifacts.is_absolute() or not private.is_dir() or not artifacts.is_dir():
            raise InvalidArchiveConfig()
        private, artifacts = private.resolve(), artifacts.resolve()
        if private == artifacts or private in artifacts.parents or artifacts in private.parents:
            raise InvalidArchiveConfig()
        if (environ['PARCHWELL_ARCHIVE_DIRECTORY'] != str(private / 'archive')
                or environ['PARCHWELL_ARCHIVE_MANIFEST_DIRECTORY'] != str(artifacts / 'archive')
                or environ['PARCHWELL_ARCHIVE_MANIFEST_PATH'] != str(artifacts / 'archive' / 'archive-manifest.json')):
            raise InvalidArchiveConfig()
        for child in (private / 'archive', artifacts / 'archive'):
            if os.path.lexists(child) and (child.is_symlink() or not child.is_dir()):
                raise InvalidArchiveConfig()
        phase = _common.read_json(private / 'phase.json')
        if not isinstance(phase, dict): raise InvalidArchiveConfig()
        config = validate_archive_config(phase['evidence_archive'])
        github = environ['GITHUB_RUN_ID']
        attempt = environ['GITHUB_RUN_ATTEMPT']
        if len(attempt) > 9 or not re.fullmatch(r'[1-9][0-9]*', attempt): raise InvalidArchiveConfig()
        identity = {'github_run_id': github, 'github_run_attempt': int(attempt), 'run_id': phase['run_id'],
                    'phase': phase['phase'], 'source_commit': phase['source_commit']}
        validate_identity(identity)
        if (environ['PARCHWELL_RUN_ID'] != identity['run_id'] or environ['GITHUB_SHA'] != identity['source_commit']
                or not identity['run_id'].endswith('-' + github + '-' + attempt)):
            raise InvalidArchiveConfig()
        try:
            ephemeral = _common.read_json(private / 'ephemeral-output.json')
        except _common.Invalid:
            return private, artifacts, phase, identity, config, None, None
        blob = _common.unwrap(ephemeral['blob'])
        source_host = _common.endpoint(blob['account_url'])
        if source_host == config['account_name'] + '.blob.core.windows.net':
            raise InvalidArchiveConfig()
        containers = blob['containers']
        if (not isinstance(containers, dict) or containers.get('ledger') != 'ledger'
                or containers.get('verifier') != 'verifier'
                or ('claim_check' in containers and containers['claim_check'] != 'claim-check')
                or (phase.get('scenario') == 'probe-batch' and containers.get('single_action') != 'single-action')
                or ('single_action' in containers and containers['single_action'] != 'single-action')
                or ('native_monitoring' in containers and containers['native_monitoring'] != 'native-monitoring')):
            raise InvalidArchiveConfig()
        return private, artifacts, phase, identity, config, source_host, containers
    except (KeyError, TypeError, ValueError, OSError, _common.Invalid):
        raise InvalidArchiveConfig() from None


def capture_name(run, phase, mode, job, pod_uid, index, repeat):
    if (mode not in ('verify', 'b10-abrupt-verify', 'b10-sweep', 'b10-final-sweep')
            or type(index) is not int or index < 0
            or any(not isinstance(value, str) or not re.fullmatch(r'[A-Za-z0-9_-]{1,128}', value) for value in (job, pod_uid))
            or not safe_name(run) or '/' in run or not safe_name(phase) or '/' in phase):
        raise InvalidArchiveConfig()
    capture_id = f'{mode}-{job}-{pod_uid}-w{index:04d}'
    if len(capture_id) > 1024:
        raise InvalidArchiveConfig()
    if mode in ('b10-sweep', 'b10-final-sweep'):
        root = f'{run}/{phase}/'
    else:
        if type(repeat) is not int or repeat < 1:
            raise InvalidArchiveConfig()
        root = f'{run}/{phase}/r{repeat}/'
    return capture_id, root + 'consumed-metadata/' + capture_id + '/'


def project_pods(data):
    """Retain bounded pod identities and lifecycle status without pod configuration."""
    value = strict_control(data, 4 * 1048576)
    if not isinstance(value, dict) or not isinstance(value.get('items'), list) or len(value['items']) > 4:
        raise InvalidArchiveConfig()
    result = []
    for pod in value['items']:
        if not isinstance(pod, dict) or not isinstance(pod.get('metadata'), dict) or not isinstance(pod.get('status'), dict):
            raise InvalidArchiveConfig()
        metadata, status = pod['metadata'], pod['status']
        name, uid = metadata.get('name'), metadata.get('uid')
        if (not isinstance(name, str) or not re.fullmatch(r'[a-z0-9](?:[a-z0-9-]*[a-z0-9])?', name)
                or len(name) > 253 or not isinstance(uid, str) or not re.fullmatch(r'[A-Za-z0-9_-]{1,128}', uid)):
            raise InvalidArchiveConfig()
        phase = status.get('phase')
        if phase not in ('Pending', 'Running', 'Succeeded', 'Failed', 'Unknown', None):
            raise InvalidArchiveConfig()
        conditions = []
        condition_rows = status.get('conditions', [])
        if not isinstance(condition_rows, list) or len(condition_rows) > 100: raise InvalidArchiveConfig()
        for condition in condition_rows:
            if not isinstance(condition, dict): raise InvalidArchiveConfig()
            kind, state = condition.get('type'), condition.get('status')
            if kind not in ('Initialized', 'Ready', 'ContainersReady', 'PodScheduled', 'PodReadyToStartContainers') or state not in ('True', 'False', 'Unknown'):
                continue
            conditions.append({'type': kind, 'status': state})
        containers = []
        for key in ('initContainerStatuses', 'containerStatuses'):
            entries = status.get(key, [])
            if not isinstance(entries, list) or len(entries) > 4: raise InvalidArchiveConfig()
            for entry in entries:
                if not isinstance(entry, dict) or not isinstance(entry.get('name'), str) or not re.fullmatch(r'[a-z0-9](?:[a-z0-9-]*[a-z0-9])?', entry['name']):
                    raise InvalidArchiveConfig()
                state = entry.get('state', {})
                if not isinstance(state, dict): raise InvalidArchiveConfig()
                terminated = state.get('terminated')
                exit_code = terminated.get('exitCode') if isinstance(terminated, dict) else None
                if exit_code is not None and type(exit_code) is not int: raise InvalidArchiveConfig()
                containers.append({'kind': key, 'name': entry['name'], 'terminated_exit_code': exit_code})
        result.append({'name': name, 'uid': uid, 'phase': phase, 'conditions': conditions, 'containers': containers})
    return _common.encoded({'schema_version': 1, 'kind': 'pod_diagnostics', 'items': result})


def publication_inventory(data, prefix, identity):
    """Read the producer's fixed index to bind each allowed publication file."""
    value = strict_control(data, 1048576)
    fields = {'schema_version', 'identity', 'worker_status', 'bookkeeping_complete', 'worker_complete', 'publication_complete', 'artifacts'}
    names = ('ledger.jsonl', 'manifest.json', 'statistics.jsonl', 'request-lifecycle.jsonl', 'capabilities.json')
    if (not isinstance(value, dict) or set(value) != fields or type(value['schema_version']) is not int
            or value['schema_version'] != 1 or value['identity'] != identity
            or not isinstance(value['worker_status'], str)
            or any(type(value[key]) is not bool for key in ('bookkeeping_complete', 'worker_complete', 'publication_complete'))
            or not isinstance(value['artifacts'], list) or len(value['artifacts']) != len(names)
            or not safe_name(prefix.rstrip('/')) or not prefix.endswith('/')):
        raise InvalidArchiveConfig()
    result = []
    for name, descriptor in zip(names, value['artifacts']):
        if (not isinstance(descriptor, dict) or set(descriptor) != {'name', 'length', 'sha256'}
                or descriptor['name'] != name or type(descriptor['length']) is not int
                or not 0 <= descriptor['length'] <= ArchiveMaxObjectBytes
                or not isinstance(descriptor['sha256'], str) or not re.fullmatch(r'[0-9a-f]{64}', descriptor['sha256'])):
            raise InvalidArchiveConfig()
        result.append({'name': prefix + name, 'bytes': descriptor['length'], 'sha256': descriptor['sha256']})
    return result


def fixed_nonledger_objects(phase, records):
    """Derive fixed verifier outputs from selected non-ledger jobs and their status."""
    scenario, run, kind = phase.get('scenario'), phase.get('run_id'), phase.get('run_kind')
    if not safe_name(run) or '/' in run or kind not in ('smoke', 'batch') or not isinstance(records, list):
        raise InvalidArchiveConfig()
    result, gaps = [], []
    def add(record, name, item, configuration):
        started = record is not None and record.get('apply') is not None
        if record is None:
            gaps.append({'code': 'monitoring_incomplete', 'boundary': 'missing job status', 'object_name': name,
                         'expected_unit': f'{item}-{configuration}'})
        result.append({'container': 'verifier', 'name': name, 'required': started,
                       'state': 'pending' if started else 'not_started', 'repeat': None})
    def find(mode, item, configuration):
        matches = [r for r in records if isinstance(r, dict) and r.get('mode') == mode
                   and r.get('item') == item and r.get('configuration') == configuration]
        if len(matches) > 1: raise InvalidArchiveConfig()
        return matches[0] if matches else None
    if scenario in ('probe-batch', 'probe-batch-2'):
        if scenario == 'probe-batch':
            matrix = (('B01','C0'),('B01','C1'),('B03','C1'),('B03','C2'),('B03','C3'),('B03','C4'),
                      ('B05','C1'),('C08','C0'),('C08','C1'),('C12','C1'),('C12','C2'),
                      ('F04','C0'),('F04','C1'),('F04','C2'),('F04','C3'),('F04','C4'))
            if kind == 'smoke': return [], []
            base, judge = 'single-action', 'single-action-judge'
        else:
            matrix = (('B08','C0'),('B08','C1'),('E01','C0'),('E01','C1'),
                      *tuple(('F06', f'C{i}') for i in range(12)))
            if phase.get('capture_authentication_metadata') is True:
                matrix = (('F06','C6'), ('F06','C9'))
            base, judge = 'single-action-v2', 'single-action-v2-judge'
        items = phase.get('items')
        if not isinstance(items, list): raise InvalidArchiveConfig()
        for item, configuration in matrix:
            if item not in items: continue
            record = find(judge, item, configuration)
            for leaf in ('candidate.json', 'judge-completion.json'):
                add(record, f'{run}/{base}/{item}/{configuration}/{leaf}', item, configuration)
    elif scenario == 'idle-observation':
        items = phase.get('items')
        if not isinstance(items, list): raise InvalidArchiveConfig()
        record = find('idle-observation-judge', None, None)
        add(record, f'{run}/idle-observation/judge-completion.json', 'judge', 'judge')
        for item in items:
            if item not in ('A02', 'A03', 'A04'): raise InvalidArchiveConfig()
            add(record, f'{run}/idle-observation/{item}/judge/candidate.json', item, 'judge')
    elif scenario == 'ledger-batch':
        if kind == 'smoke': return [], []
        for item in ('B06', 'B07'):
            for configuration in ('C0', 'C1'):
                record = find('judge', item, configuration)
                add(record, f'{run}-{item.lower()}/{configuration}/candidate.json', item, configuration)
    else:
        raise InvalidArchiveConfig()
    return sorted(result, key=lambda item: item['name']), gaps


class Collector:
    """Bounded data-object accounting shared by inventory adapters."""
    capture_name = staticmethod(capture_name)
    project_pods = staticmethod(project_pods)
    def __init__(self, transfer, prefix):
        if not prefix.endswith('/') or not safe_name(prefix.rstrip('/')):
            raise InvalidArchiveConfig()
        self.transfer, self.prefix = transfer, prefix
        self.objects, self.captures, self.gaps = [], [], []
        self.window_sources = {}
        self.native_existing = {}
        self.names = set()
        self.verified_bytes = 0
        self.logical_bytes = 0

    def gap(self, code, boundary, name=None, unit=None):
        gap = {'code': code, 'boundary': boundary, 'object_name': name, 'expected_unit': unit}
        validate_gaps([gap])
        if len(self.gaps) < 100:
            self.gaps.append(gap)
        elif self.gaps[-1]['code'] != 'diagnostics_truncated':
            self.gaps[-1] = {'code': 'diagnostics_truncated', 'boundary': 'diagnostic count', 'object_name': None, 'expected_unit': None}

    def source(self, container, name, kind, repeat=None, capture_id=None, required=True,
               expected_length=None, expected_sha256=None, inspect_source=None, max_object=ArchiveMaxObjectBytes, source_properties=None):
        if len(self.objects) >= ArchiveMaxObjects - 2:
            self.gap('object_limit', 'data object count', name)
            return None
        if not safe_name(container) or '/' in container or not safe_name(name):
            raise InvalidArchiveConfig()
        if container == 'verifier' and inspect_source is None and (name.endswith('/start-line.json') or name.endswith('/result.json')
                or name.endswith('/nonfinal-result.json') or name.endswith('/sweep-line.json')):
            def inspect_window(path, size):
                try:
                    spec = importlib.util.spec_from_file_location('parchwell_archive_json', Path(__file__).with_name('archive_json.py'))
                    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
                    self.window_sources[name] = module.windows(path, self.transfer.deadline)
                except TimeoutError:
                    raise Gap('deadline', 'source window') from None
                except Exception:
                    self.window_sources[name] = None
            inspect_source = inspect_window
        if name.endswith('/statistics.jsonl'):
            previous_inspect = inspect_source
            def inspect_statistics(path, size):
                if previous_inspect is not None:
                    previous_inspect(path, size)
                import hashlib
                refs = {}
                try:
                    with open(path, 'rb') as stream:
                        for ordinal in range(1, 4098):
                            _common.check_deadline(self.transfer.deadline, time.monotonic)
                            line = stream.readline(MetadataChunkMaxBytes + 1)
                            if not line: break
                            if ordinal > 4096 or not line.endswith(b'\n') or len(line) > MetadataChunkMaxBytes:
                                return
                            row = strict_control(line, MetadataChunkMaxBytes)
                            raw = row['raw_json'].encode('utf-8')
                            client_id = row['identity']['client_id']
                            if len(raw) > MetadataRowMaxBytes or not isinstance(client_id, str):
                                return
                            refs[(client_id, ordinal)] = (len(raw), hashlib.sha256(raw).hexdigest())
                except (OSError, ValueError, KeyError, TypeError, UnicodeError):
                    return
                self.native_existing[(container, name)] = refs
            inspect_source = inspect_statistics
        destination = f'{self.prefix}objects/{container}/{name}'
        if destination in self.names: raise InvalidArchiveConfig()
        self.names.add(destination)
        obj = {'kind': kind, 'source': {'container': container, 'name': name, 'etag': None},
               'destination_name': destination, 'required': required, 'repeat': repeat, 'capture_id': capture_id,
               'bytes': None, 'sha256': None, 'destination_etag': None, 'state': 'failed', 'gap_code': None}
        self.objects.append(obj)
        try:
            remaining = ArchiveMaxBytes - 1048576 - 65536 - self.logical_bytes
            size, sha, source_etag, destination_etag = self.transfer.copy(
                container, name, destination, remaining, max_object=max_object,
                expected_sha256=expected_sha256, expected_length=expected_length, inspect_source=inspect_source,
                reserve=lambda size: setattr(self, "logical_bytes", self.logical_bytes + size), source_properties=source_properties)
            obj.update(bytes=size, sha256=sha, destination_etag=destination_etag, state='verified')
            obj['source']['etag'] = source_etag
            self.verified_bytes += size
            return obj
        except Gap as error:
            obj.update(state='missing' if error.code == 'source_missing' else 'failed', gap_code=error.code)
            retained = getattr(error, 'retained', None)
            if retained is not None:
                size, sha, source_etag, destination_etag = retained
                obj.update(bytes=size, sha256=sha, destination_etag=destination_etag)
                obj['source']['etag'] = source_etag
            self.gap(error.code, error.boundary, name)
        except Exception:
            obj.update(state='failed', gap_code='transport')
            self.gap('transport', 'source transfer', name)
        except KeyboardInterrupt:
            obj.update(state='failed', gap_code='interrupted')
            self.gap('interrupted', 'source transfer', name)
            raise
        return None

    def reject_binding(self, obj, code, boundary):
        if obj is not None and obj['state'] == 'verified':
            obj.update(state='rejected', gap_code=code)
            self.verified_bytes -= obj['bytes']
        self.gap(code, boundary, obj['source']['name'] if obj is not None else None)

    def projected_runner(self, name, content):
        if len(self.objects) >= ArchiveMaxObjects - 2:
            self.gap('object_limit', 'data object count', name); return
        if not safe_name(name): raise InvalidArchiveConfig()
        destination = f'{self.prefix}runner/{name}.metadata.json'
        if destination in self.names: raise InvalidArchiveConfig()
        self.names.add(destination)
        obj = {'kind': 'source_metadata_projection', 'source': {'container': 'runner', 'name': name, 'etag': None},
               'destination_name': destination, 'required': True, 'repeat': None, 'capture_id': None,
               'bytes': None, 'sha256': None, 'destination_etag': None, 'state': 'failed', 'gap_code': None}
        self.objects.append(obj)
        temporary = self.transfer.spool / 'v2-projection.json'
        try:
            if len(content) > ArchiveMaxObjectBytes: raise Gap('source_too_large', 'projection size')
            _common.safe_write(temporary, content)
            remaining = ArchiveMaxBytes - 1048576 - 65536 - self.logical_bytes
            size, sha, etag = self.transfer.copy_local(temporary, destination, remaining,
                reserve=lambda count: setattr(self, 'logical_bytes', self.logical_bytes + count))
            obj.update(bytes=size, sha256=sha, destination_etag=etag, state='verified')
            self.verified_bytes += size
        except Gap as error:
            obj.update(gap_code=error.code); self.gap(error.code, error.boundary, name)
        except KeyboardInterrupt:
            obj.update(gap_code='interrupted'); self.gap('interrupted', 'source projection', name); raise
        except Exception:
            obj.update(gap_code='transport'); self.gap('transport', 'source projection', name)
        finally:
            temporary.unlink(missing_ok=True)

    def unavailable_projection(self, name, code='monitoring_incomplete'):
        if len(self.objects) >= ArchiveMaxObjects - 2:
            self.gap('object_limit', 'data object count', name); return
        if not safe_name(name): raise InvalidArchiveConfig()
        destination = f'{self.prefix}runner/{name}.metadata.json'
        if destination in self.names: raise InvalidArchiveConfig()
        self.names.add(destination)
        self.objects.append({'kind': 'source_metadata_projection', 'source': {'container': 'runner', 'name': name, 'etag': None},
                             'destination_name': destination, 'required': True, 'repeat': None, 'capture_id': None,
                             'bytes': None, 'sha256': None, 'destination_etag': None, 'state': 'failed', 'gap_code': code})
        self.gap(code, 'v2 actor stdout or collection incomplete', name)

    def legacy_source(self, container, name, kind, mode, identity, run_kind):
        valid = []
        def inspect(path, size):
            try:
                spec = importlib.util.spec_from_file_location('parchwell_archive_json', Path(__file__).with_name('archive_json.py'))
                module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
                document = module.project(path, self.transfer.deadline)
                if not legacy_closed(document, kind, mode):
                    raise Gap('monitoring_incomplete', 'source publication shape')
                valid.append(validate_legacy_document(document, kind, mode, identity, run_kind))
            except TimeoutError:
                raise Gap('deadline', 'source contract') from None
            except Gap:
                raise
            except Exception:
                raise Gap('monitoring_incomplete', 'source publication shape') from None
        obj = self.source(container, name, kind, inspect_source=inspect)
        if obj is not None and valid != [True]:
            self.reject_binding(obj, 'monitoring_incomplete', 'source publication contract')
        return obj

    def legacy_capture_projection(self, container, name, mode, identity, run_kind):
        if len(self.objects) >= ArchiveMaxObjects - 2:
            self.gap('object_limit', 'data object count', name); return
        if not safe_name(container) or not safe_name(name): raise InvalidArchiveConfig()
        destination = f'{self.prefix}objects/{container}/{name}.metadata.json'
        if destination in self.names: raise InvalidArchiveConfig()
        self.names.add(destination)
        obj = {'kind': 'source_metadata_projection', 'source': {'container': container, 'name': name, 'etag': None},
               'destination_name': destination, 'required': True, 'repeat': None, 'capture_id': None,
               'bytes': None, 'sha256': None, 'destination_etag': None, 'state': 'failed', 'gap_code': None}
        self.objects.append(obj)
        try:
            remaining = ArchiveMaxBytes - 1048576 - 65536 - self.logical_bytes
            def validate(doc):
                valid = validate_capture_projection(doc, mode, identity, run_kind)
                if valid:
                    refs = {}
                    for repeat in doc['repeats']:
                        for row in repeat['statistics']:
                            refs[(repeat['client_id'], row['sequence'])] = (row['raw_base64'][1], row['raw_base64'][2])
                    self.native_existing[(container, name)] = refs
                return valid
            size, sha, etag, dest_etag, _, valid = self.transfer.project_source(
                container, name, destination, remaining,
                validate,
                reserve=lambda count: setattr(self, 'logical_bytes', self.logical_bytes + count))
            obj.update(bytes=size, sha256=sha, destination_etag=dest_etag, state='verified')
            obj['source']['etag'] = etag; self.verified_bytes += size
            if not valid: self.reject_binding(obj, 'monitoring_incomplete', 'source publication contract')
        except Gap as error:
            obj.update(state='missing' if error.code == 'source_missing' else 'failed', gap_code=error.code)
            self.gap(error.code, error.boundary, name)
        except KeyboardInterrupt:
            obj.update(gap_code='interrupted'); self.gap('interrupted', 'source projection', name); raise
        except Exception:
            obj.update(gap_code='transport'); self.gap('transport', 'source projection', name)

    def v2_candidate_projection(self, container, name, identity, expected_sha256):
        if len(self.objects) >= ArchiveMaxObjects - 2:
            self.gap('object_limit', 'data object count', name); return
        if not safe_name(container) or not safe_name(name): raise InvalidArchiveConfig()
        destination = f'{self.prefix}objects/{container}/{name}.metadata.json'
        if destination in self.names: raise InvalidArchiveConfig()
        self.names.add(destination)
        obj = {'kind': 'source_metadata_projection', 'source': {'container': container, 'name': name, 'etag': None},
               'destination_name': destination, 'required': True, 'repeat': None, 'capture_id': None,
               'bytes': None, 'sha256': None, 'destination_etag': None, 'state': 'failed', 'gap_code': None}
        self.objects.append(obj)
        def validate(doc):
            return (doc['schema_version'] == 2 and doc['mode'] == 'single-action-v2'
                    and doc['synthetic'] is True and doc['adr012_counts'] == 'not_applicable'
                    and all(doc[k] == v for k, v in identity.items()))
        try:
            remaining = ArchiveMaxBytes - 1048576 - 65536 - self.logical_bytes
            size, sha, etag, dest_etag, _, _ = self.transfer.project_source(
                container, name, destination, remaining, validate,
                reserve=lambda count: setattr(self, 'logical_bytes', self.logical_bytes + count),
                projection_format='probe_v2_candidate', expected_sha256=expected_sha256)
            obj.update(bytes=size, sha256=sha, destination_etag=dest_etag, state='verified')
            obj['source']['etag'] = etag; self.verified_bytes += size
        except Gap as error:
            obj.update(state='missing' if error.code == 'source_missing' else 'failed', gap_code=error.code)
            self.gap(error.code, error.boundary, name)
        except KeyboardInterrupt:
            obj.update(gap_code='interrupted'); self.gap('interrupted', 'source projection', name); raise
        except Exception:
            obj.update(gap_code='transport'); self.gap('transport', 'source projection', name)

    def optional_source(self, container, name, kind, repeat=None):
        try:
            if shutil.disk_usage(self.transfer.spool).free < 2 * ArchiveMaxObjectBytes + 1048576 + 65536:
                raise Gap('disk_space', 'spool reserve')
            properties = self.transfer.properties(self.transfer.source_account, container, name, missing_ok=True)
        except Gap as error:
            self.gap(error.code, error.boundary, name)
            return
        if properties is not None:
            self.source(container, name, kind, repeat, source_properties=properties)

    def runner(self, name, source_path, kind):
        if len(self.objects) >= ArchiveMaxObjects - 2:
            self.gap('object_limit', 'data object count', name)
            return
        if not safe_name(name): raise InvalidArchiveConfig()
        destination = f'{self.prefix}runner/{name}'
        if destination in self.names: raise InvalidArchiveConfig()
        self.names.add(destination)
        obj = {'kind': kind, 'source': {'container': 'runner', 'name': name, 'etag': None},
               'destination_name': destination, 'required': True, 'repeat': None, 'capture_id': None,
               'bytes': None, 'sha256': None, 'destination_etag': None, 'state': 'failed', 'gap_code': None}
        self.objects.append(obj)
        try:
            remaining = ArchiveMaxBytes - 1048576 - 65536 - self.logical_bytes
            size, sha, etag = self.transfer.copy_local(source_path, destination, remaining,
                reserve=lambda size: setattr(self, 'logical_bytes', self.logical_bytes + size))
            obj.update(bytes=size, sha256=sha, destination_etag=etag, state='verified')
            self.verified_bytes += size
        except Gap as error:
            obj.update(gap_code=error.code)
            self.gap(error.code, error.boundary, name)
        except Exception:
            obj.update(gap_code='transport')
            self.gap('transport', 'runner transfer', name)
        except KeyboardInterrupt:
            obj.update(gap_code='interrupted')
            self.gap('interrupted', 'runner transfer', name)
            raise

    def producer(self, container, run, phase, repeat, job, pod, uid, source_commit):
        root = f'{run}/{phase}/r{repeat}/producer/{pod}/'
        identities = []
        def inspect(path, size):
            if size > 1048576:
                self.gap('monitoring_incomplete', 'producer registration size', root + 'registration.json')
                return
            try:
                fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
                with os.fdopen(fd, 'rb') as stream: value = strict_control(stream.read(1048577), 1048576)
                identity = value['identity']
                if (not isinstance(identity, dict) or set(identity) != {'scenario_id', 'configuration_id', 'run_id', 'instance_id', 'expected_instance_id', 'client_id'}
                        or identity['run_id'] != run or value['phase'] != phase or value['repeat_number'] != repeat
                        or value['job_name'] != job or value['pod_name'] != pod or value['pod_uid'] != uid
                        or value['source_commit'] != source_commit):
                    raise InvalidArchiveConfig()
                identities.append(identity)
            except (OSError, InvalidArchiveConfig, KeyError, TypeError):
                self.gap('monitoring_incomplete', 'producer registration identity', root + 'registration.json')
        self.source(container, root + 'registration.json', 'producer_registration', repeat, inspect_source=inspect)
        self.source(container, root + 'completion.json', 'producer_completion', repeat)
        self.publication(container, root + 'publication/', identities[0] if identities else None, repeat)

    def abrupt(self, container, run, repeat, pod, role, diagnostic=False, termination_validator=None,
               manifest_validator=None, successor_validator=None, first_pod=None, registration_validator=None):
        if role not in ('first', 'successor') or not safe_name(pod) or '/' in pod:
            raise InvalidArchiveConfig()
        segment = 'b10-abrupt-diagnostic' if diagnostic else 'b10-abrupt'
        prefix = f'{run}/C1/r{repeat}/{segment}/{pod}/'
        def descriptor(value, name):
            if (not isinstance(value, dict) or set(value) != {'name', 'length', 'sha256'}
                    or value['name'] != name or type(value['length']) is not int
                    or not 0 <= value['length'] <= ArchiveMaxObjectBytes
                    or not isinstance(value['sha256'], str) or not re.fullmatch(r'[0-9a-f]{64}', value['sha256'])):
                raise InvalidArchiveConfig()
            return value
        def read_control(path, size, validator):
            if validator is None or size > 65536: raise InvalidArchiveConfig()
            fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
            with os.fdopen(fd, 'rb') as stream: raw = stream.read(65537)
            return validator(raw)['data']
        registration_bad = []
        def inspect_registration(path, size):
            try:
                read_control(path, size, registration_validator)
            except Gap:
                raise
            except Exception:
                registration_bad.append(True)
        if role == 'successor':
            names = ('ledger.jsonl', 'manifest.json', 'statistics.jsonl', 'request-lifecycle.jsonl', 'capabilities.json')
            declared = {}; completion_bad = []
            def inspect_successor(path, size):
                try:
                    data = read_control(path, size, successor_validator)
                    declared['registration.json'] = descriptor(data['registration'], 'registration.json')
                    if not isinstance(data['artifacts'], list) or len(data['artifacts']) != len(names): raise InvalidArchiveConfig()
                    for item in data['artifacts']:
                        if not isinstance(item, dict) or item.get('name') not in names or item['name'] in declared: raise InvalidArchiveConfig()
                        declared[item['name']] = descriptor(item, item['name'])
                    if set(declared) != set(names) | {'registration.json'}: raise InvalidArchiveConfig()
                    for key, leaf in (('first_termination', 'termination.json'), ('journal', 'journal.jsonl')):
                        reference = descriptor(data[key], leaf)
                        source = f'{run}/C1/r{repeat}/{segment}/{first_pod}/{leaf}'
                        prior = next((obj for obj in self.objects if obj['source']['container'] == container and obj['source']['name'] == source), None)
                        if prior is None or prior['state'] == 'missing':
                            self.gap('source_missing', 'predecessor reference', source)
                        elif prior['state'] != 'verified':
                            self.gap('monitoring_incomplete', 'predecessor reference unverified', source)
                        elif prior['bytes'] != reference['length']:
                            self.reject_binding(prior, 'source_length_mismatch', 'predecessor reference')
                        elif prior['sha256'] != reference['sha256']:
                            self.reject_binding(prior, 'source_hash_mismatch', 'predecessor reference')
                except Gap:
                    raise
                except Exception:
                    declared.clear(); completion_bad.append(True)
            completion_obj = self.source(container, prefix + 'completion.json', 'abrupt_successor', repeat, inspect_source=inspect_successor)
            if completion_bad: self.reject_binding(completion_obj, 'monitoring_incomplete', 'successor completion contract')
            for name in ('registration.json', *names):
                ref = declared.get(name, {})
                obj = self.source(container, prefix + name, 'abrupt_successor', repeat,
                                  expected_length=ref.get('length'), expected_sha256=ref.get('sha256'),
                                  inspect_source=inspect_registration if name == 'registration.json' else None)
                if name == 'registration.json' and registration_bad:
                    self.reject_binding(obj, 'monitoring_incomplete', 'abrupt registration contract')
            return False
        reports = ('same_instance-pre-acceptance-report.json', 'control-post-drain-report.json',
                   'cross_restart-pre-acceptance-report.json')
        declared = {}; journals = []; manifest_bad = []
        def inspect(path, size):
            try:
                data = read_control(path, size, manifest_validator)
                journals.append((descriptor(data['journal'], 'journal.jsonl'), data['journal_records']))
                if type(data['journal_records']) is not int or data['journal_records'] < 0: raise InvalidArchiveConfig()
                entries = data['report_files']
                if not isinstance(entries, list) or len(entries) > len(reports): raise InvalidArchiveConfig()
                for entry in entries:
                    if not isinstance(entry, dict) or entry.get('name') not in reports or entry['name'] in declared: raise InvalidArchiveConfig()
                    declared[entry['name']] = descriptor(entry, entry['name'])
            except Gap:
                raise
            except Exception:
                journals.clear(); declared.clear(); manifest_bad.append(True)
        registration_obj = self.source(container, prefix + 'registration.json', 'abrupt_first', repeat, inspect_source=inspect_registration)
        if registration_bad: self.reject_binding(registration_obj, 'monitoring_incomplete', 'abrupt registration contract')
        manifest_obj = self.source(container, prefix + 'abrupt-manifest.json', 'abrupt_first', repeat, inspect_source=inspect)
        if manifest_bad: self.reject_binding(manifest_obj, 'monitoring_incomplete', 'abrupt manifest contract')
        journal_count_failed = []
        def inspect_journal(path, size):
            if not journals: return
            try:
                fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
                count = 0; last = b''
                with os.fdopen(fd, 'rb') as stream:
                    while True:
                        _common.check_deadline(self.transfer.deadline, time.monotonic)
                        chunk = stream.read(65536)
                        if not chunk: break
                        count += chunk.count(b'\n'); last = chunk[-1:]
                if count != journals[0][1] or (size and last != b'\n'):
                    journal_count_failed.append(True)
            except OSError:
                self.gap('transport', 'abrupt journal count', prefix + 'journal.jsonl')
        journal = journals[0][0] if journals else {}
        journal_obj = self.source(container, prefix + 'journal.jsonl', 'abrupt_first', repeat,
                                  expected_length=journal.get('length'), expected_sha256=journal.get('sha256'), inspect_source=inspect_journal)
        if journal_count_failed:
            self.reject_binding(journal_obj, 'monitoring_incomplete', 'abrupt journal count')
        terminations = []; termination_bad = []
        def inspect_termination(path, size):
            try:
                value = read_control(path, size, termination_validator)
                ref = descriptor(value['manifest'], 'abrupt-manifest.json')
                prior = next((obj for obj in self.objects if obj['source']['container'] == container
                              and obj['source']['name'] == prefix + 'abrupt-manifest.json'), None)
                if prior is None or prior['state'] == 'missing':
                    self.gap('source_missing', 'first manifest reference', prefix + 'abrupt-manifest.json')
                elif prior['state'] != 'verified':
                    self.gap('monitoring_incomplete', 'first manifest reference unverified', prefix + 'abrupt-manifest.json')
                elif prior['bytes'] != ref['length']:
                    self.reject_binding(prior, 'source_length_mismatch', 'first manifest reference')
                elif prior['sha256'] != ref['sha256']:
                    self.reject_binding(prior, 'source_hash_mismatch', 'first manifest reference')
                terminations.append(value['termination'])
            except Gap:
                raise
            except Exception:
                termination_bad.append(True)
        termination = self.source(container, prefix + 'termination.json', 'abrupt_first', repeat, inspect_source=inspect_termination)
        if termination_bad: self.reject_binding(termination, 'monitoring_incomplete', 'abrupt termination contract')
        for name in reports:
            if name in declared:
                self.source(container, prefix + name, 'abrupt_report', repeat, expected_length=declared[name]['length'], expected_sha256=declared[name]['sha256'])
            else:
                self.optional_source(container, prefix + name, 'abrupt_report', repeat)
        return termination is not None and terminations == ['no_kill']

    def publication(self, container, prefix, identity, repeat):
        parsed = []
        def inspect(path, size):
            if size > 1048576:
                self.gap('monitoring_incomplete', 'publication index size', prefix + 'index.json')
                return
            try:
                fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
                with os.fdopen(fd, 'rb') as stream:
                    data = stream.read(1048577)
                parsed.extend(publication_inventory(data, prefix, identity))
            except (OSError, InvalidArchiveConfig):
                self.gap('monitoring_incomplete', 'publication index contract', prefix + 'index.json')
        self.source(container, prefix + 'index.json', 'producer_index', repeat, inspect_source=inspect)
        if not parsed:
            parsed = [{'name': prefix + name, 'bytes': None, 'sha256': None} for name in
                      ('ledger.jsonl', 'manifest.json', 'statistics.jsonl', 'request-lifecycle.jsonl', 'capabilities.json')]
        for descriptor in parsed:
            self.source(container, descriptor['name'], 'producer_publication', repeat,
                        expected_length=descriptor['bytes'], expected_sha256=descriptor['sha256'])

    def expected_capture_window(self, identity, mode, repeat, capture_id):
        run, phase = identity['run_id'], identity['phase']
        def vector(name, field, coordinate):
            obj = next((item for item in self.objects if item['source']['container'] == 'verifier'
                        and item['source']['name'] == name), None)
            data = self.window_sources.get(name)
            if obj is None or obj['state'] != 'verified' or not isinstance(data, dict): raise InvalidArchiveConfig()
            if name.endswith('/sweep-line.json'):
                if (data.get('run_id') != run or data.get('phase') != phase or data.get('scenario_id') != identity['scenario_id']
                        or data.get('configuration_id') != identity['configuration_id'] or data.get('source_commit') != identity['source_commit']
                        or data.get('topic') != 'document-events'):
                    raise InvalidArchiveConfig()
            elif name.endswith('/nonfinal-result.json'):
                source_identity = data.get('identity')
                if (not isinstance(source_identity, dict) or data.get('kind') != 'nonfinal_result'
                        or source_identity.get('run_id') != run or source_identity.get('phase') != phase
                        or source_identity.get('repeat') != repeat or source_identity.get('scenario_id') != identity['scenario_id']
                        or source_identity.get('configuration_id') != identity['configuration_id']
                        or source_identity.get('source_commit') != identity['source_commit']
                        or source_identity.get('job_name') != identity['job_name'] or source_identity.get('pod_uid') != identity['pod_uid']):
                    raise InvalidArchiveConfig()
            else:
                source_identity = data.get('identity')
                if (not isinstance(source_identity, dict) or source_identity.get('run_id') != run
                        or source_identity.get('scenario_id') != identity['scenario_id']
                        or source_identity.get('configuration_id') != identity['configuration_id']
                        or data.get('verifier_commit') != identity['source_commit']):
                    raise InvalidArchiveConfig()
                if name.endswith('/start-line.json'):
                    match = re.fullmatch(re.escape(f'{run}/{phase}/') + r'r([1-5])/start-line\.json', name)
                    if match is None: raise InvalidArchiveConfig()
                    job = f'{run}-{phase.lower()}-r{match.group(1)}-loadgen'
                    if data.get('topic') != 'document-events' or any(source_identity.get(key) != job for key in ('instance_id', 'expected_instance_id', 'client_id')):
                        raise InvalidArchiveConfig()
            if field not in data and isinstance(data.get('data'), dict): data = data['data']
            values = data.get(field)
            if not isinstance(values, list) or not values: raise InvalidArchiveConfig()
            result = []
            for item in values:
                if (not isinstance(item, dict) or set(item) != {'partition', coordinate}
                        or type(item['partition']) is not int or item['partition'] < 0
                        or type(item[coordinate]) is not int or item[coordinate] < 0): raise InvalidArchiveConfig()
                result.append({'partition': item['partition'], 'offset': item[coordinate]})
            if [item['partition'] for item in result] != sorted({item['partition'] for item in result}): raise InvalidArchiveConfig()
            return result
        if mode == 'b10-final-sweep':
            candidates = sorted(name for name in self.window_sources if re.fullmatch(re.escape(f'{run}/{phase}/') + r'r[1-5]/start-line\.json', name))
            if not candidates: raise InvalidArchiveConfig()
            return vector(candidates[0], 'partition_highs', 'high'), vector(f'{run}/{phase}/sweep-line.json', 'partition_highs', 'high')
        if type(repeat) is not int: raise InvalidArchiveConfig()
        root = f'{run}/{phase}/r{repeat}/'
        start = vector(root + 'start-line.json', 'partition_highs', 'high')
        result_name = root + ('b10-abrupt/nonfinal-result.json' if mode == 'b10-abrupt-verify' else 'result.json')
        if mode == 'b10-abrupt-verify' and f'{run}/C1/r{repeat}/b10-abrupt-diagnostic/nonfinal-result.json' in self.window_sources:
            result_name = root + 'b10-abrupt-diagnostic/nonfinal-result.json'
        recorded_start = vector(result_name, 'start_line', 'offset')
        end = vector(result_name, 'end_line', 'offset')
        if start != recorded_start: raise InvalidArchiveConfig()
        if mode == 'b10-sweep' and int(capture_id.rsplit('-w', 1)[1]) % 2:
            return end, vector(f'{run}/{phase}/sweep-line.json', 'partition_highs', 'high')
        return start, end

    def capture(self, container, prefix, identity, mode, repeat, capture_id):
        parsed = []; binding_failed = []
        def inspect(path, size):
            if size > 1048576:
                return
            try:
                fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
                with os.fdopen(fd, 'rb') as stream:
                    data = stream.read(1048577)
                summary, chunks = capture_inventory(data, prefix, identity, mode, repeat, capture_id)
                if summary['complete']:
                    try:
                        expected_start, expected_end = self.expected_capture_window(identity, mode, repeat, capture_id)
                        window = strict_control(data, 1048576)['window']
                        if window['start'] != expected_start or window['end'] != expected_end: raise InvalidArchiveConfig()
                    except InvalidArchiveConfig:
                        binding_failed.append(True)
                        summary['complete'] = False
                parsed.append((summary, chunks))
            except (OSError, InvalidArchiveConfig):
                pass
        obj = self.source(container, prefix + 'capture.json', 'consumed_capture', repeat, capture_id,
                          inspect_source=inspect)
        if obj is None:
            if self.gaps and self.gaps[-1]['code'] == 'source_missing':
                self.gaps[-1]['code'] = 'capture_missing'
                self.objects[-1]['gap_code'] = 'capture_missing'
            self.captures.append({'capture_id': capture_id, 'mode': mode, 'repeat': repeat, 'required': True,
                                  'complete': False, 'rows': None, 'first_missing_ordinal': None})
            return
        if not parsed:
            self.reject_binding(obj, 'capture_incomplete', 'capture manifest contract')
            self.captures.append({'capture_id': capture_id, 'mode': mode, 'repeat': repeat, 'required': True,
                                  'complete': False, 'rows': None, 'first_missing_ordinal': None})
            return
        capture, chunks = parsed[0]
        self.captures.append(capture)
        if binding_failed:
            self.reject_binding(obj, 'capture_incomplete', 'capture window binding')
        elif not capture['complete']:
            self.gap('capture_incomplete', 'capture incomplete', prefix + 'capture.json')
        for chunk in chunks:
            self.source(container, chunk['name'], 'consumed_metadata', repeat, capture_id,
                        expected_length=chunk['bytes'], expected_sha256=chunk['sha256'], max_object=MetadataChunkMaxBytes)

    def not_started(self, container, name, kind, repeat=None, capture_id=None):
        if len(self.objects) >= ArchiveMaxObjects - 2:
            self.gap('object_limit', 'data object count', name)
            return
        if not safe_name(container) or not safe_name(name): raise InvalidArchiveConfig()
        destination = f'{self.prefix}objects/{container}/{name}'
        if destination in self.names: raise InvalidArchiveConfig()
        self.names.add(destination)
        self.objects.append({'kind': kind, 'source': {'container': container, 'name': name, 'etag': None},
                             'destination_name': destination, 'required': False,
                             'repeat': repeat, 'capture_id': capture_id, 'bytes': None, 'sha256': None,
                             'destination_etag': None, 'state': 'not_started', 'gap_code': None})

    def totals(self):
        return {'expected_objects': sum(obj['required'] for obj in self.objects),
                'verified_objects': sum(obj['state'] == 'verified' for obj in self.objects),
                'verified_bytes': self.verified_bytes}


def verify_controls(manifest_bytes, marker_bytes, receipt_bytes):
    """Require exact recovered controls and digest bindings before accepting success."""
    import hashlib
    manifest = strict_control(manifest_bytes, 1048576)
    marker = strict_control(marker_bytes, 65536)
    receipt = strict_control(receipt_bytes, 65536)
    identity = validate_manifest(manifest)
    validate_marker(marker)
    validate_receipt(receipt)
    prefix = manifest['destination']['prefix']
    if (not manifest['archive_complete'] or not receipt['archive_complete']
            or marker['identity'] != identity or receipt['identity'] != identity
            or marker['totals'] != manifest['totals'] or marker['manifest'] != receipt['manifest']
            or marker['manifest']['name'] != prefix + 'archive-manifest.json'
            or marker['manifest']['bytes'] != len(manifest_bytes)
            or marker['manifest']['sha256'] != hashlib.sha256(manifest_bytes).hexdigest()
            or receipt['completion']['name'] != prefix + 'archive-completion.json'
            or receipt['completion']['bytes'] != len(marker_bytes)
            or receipt['completion']['sha256'] != hashlib.sha256(marker_bytes).hexdigest()):
        raise InvalidArchiveConfig()
    return manifest


def read_job_pods(collector, artifacts, job, record, skip_logs=False, v2=False):
    """Archive bounded diagnostics and require the collector's declared inventory."""
    folder = Path(artifacts) / job
    projected = collector.transfer.spool / 'pod-projection.json'
    status_projection = collector.transfer.spool / 'collection-status-projection.json'
    if (record.get('collect') != 0 or type(record.get('collect')) is not int
            or record.get('collect_gap', False) is not False or record.get('outcome') == 'collect_error'):
        collector.gap('monitoring_incomplete', 'job collection failed', unit=job)
    try:
        fd = os.open(folder / 'collection-status.json', os.O_RDONLY | os.O_NOFOLLOW)
        with os.fdopen(fd, 'rb') as stream:
            status = strict_control(stream.read(1048577), 1048576)
        fields = {'complete', 'gaps'} if v2 else {'complete', 'smoke_marker_valid', 'gaps'}
        if (not isinstance(status, dict) or set(status) != fields or type(status['complete']) is not bool
                or not isinstance(status['gaps'], list) or len(status['gaps']) > 100
                or (not v2 and type(status['smoke_marker_valid']) is not bool)):
            raise InvalidArchiveConfig()
        # Only fixed vocabulary is retained; collector error records may contain
        # identifiers and must never be copied as an unvalidated document.
        allowed = {'pod_read_failed', 'invalid_pod_json', 'omitted_pod', 'no_pods', 'invalid_containers',
                   'omitted_container', 'total_byte_limit', 'truncated_log', 'log_read_failed',
                   'smoke_pod_or_container_missing_or_extra', 'smoke_marker_mismatch', 'pod_collection'}
        kinds = []
        for gap in status['gaps']:
            if not isinstance(gap, dict) or gap.get('kind') not in allowed: raise InvalidArchiveConfig()
            kind = gap['kind']
            if v2:
                fields_by_kind = {'pod_collection': {'kind'}, 'log_read_failed': {'kind', 'status'},
                                  'truncated_log': {'kind', 'status', 'limit', 'observed', 'observed_at_least', 'allowed'}}
            else:
                fields_by_kind = {'pod_read_failed': {'kind', 'status'}, 'invalid_pod_json': {'kind'},
                    'omitted_pod': {'kind', 'pod'}, 'no_pods': {'kind'}, 'invalid_containers': {'kind', 'pod'},
                    'omitted_container': {'kind', 'pod', 'container'}, 'total_byte_limit': {'kind', 'pod', 'container'},
                    'truncated_log': {'kind', 'pod', 'container', 'status'},
                    'log_read_failed': {'kind', 'pod', 'container', 'status'},
                    'smoke_pod_or_container_missing_or_extra': {'kind'}, 'smoke_marker_mismatch': {'kind'}}
            if set(gap) != fields_by_kind.get(kind): raise InvalidArchiveConfig()
            if any(type(gap[k]) is not int for k in ('status', 'observed', 'allowed') if k in gap): raise InvalidArchiveConfig()
            if any(not isinstance(gap[k], str) or not re.fullmatch(r'[a-z0-9](?:[a-z0-9-]*[a-z0-9])?', gap[k])
                   for k in ('pod', 'container') if k in gap): raise InvalidArchiveConfig()
            if kind == 'truncated_log' and v2 and (gap['limit'] != 'container_log_bytes' or gap['observed_at_least'] is not True):
                raise InvalidArchiveConfig()
            kinds.append(dict(gap))
        safe_status = {'complete': status['complete'], 'gaps': kinds}
        if not v2: safe_status['smoke_marker_valid'] = status['smoke_marker_valid']
        _common.safe_write(status_projection, _common.encoded(safe_status))
        collector.runner(f'{job}/collection-status.json', status_projection, 'collection_status')
        if status['complete'] is not True or status['gaps']:
            collector.gap('monitoring_incomplete', 'job collection incomplete', unit=job)
    except (OSError, InvalidArchiveConfig):
        collector.gap('monitoring_incomplete', 'job collection status unavailable', unit=job)
    try:
        fd = os.open(folder / 'pods.json', os.O_RDONLY | os.O_NOFOLLOW)
        with os.fdopen(fd, 'rb') as stream:
            data = stream.read(4 * 1048576 + 1)
        raw = strict_control(data, 4 * 1048576)
        projection = project_pods(data)
        value = strict_control(projection, 4 * 1048576)
        if not value['items'] or any(not pod['name'].startswith(job + '-') for pod in value['items']):
            raise InvalidArchiveConfig()
        if len({pod['name'] for pod in value['items']}) != len(value['items']): raise InvalidArchiveConfig()
        expected = []
        for pod in raw['items']:
            name = pod['metadata']['name']
            if v2:
                role = record.get('role')
                if not isinstance(role, str) or not re.fullmatch(r'[a-z0-9](?:[a-z0-9-]*[a-z0-9])?', role):
                    raise InvalidArchiveConfig()
                expected.append((name, 'containers', role))
            else:
                spec = pod.get('spec')
                if not isinstance(spec, dict): raise InvalidArchiveConfig()
                declared = []
                for kind in ('initContainers', 'containers'):
                    entries = spec.get(kind, [])
                    if not isinstance(entries, list): raise InvalidArchiveConfig()
                    for entry in entries:
                        container = entry.get('name') if isinstance(entry, dict) else None
                        if not isinstance(container, str) or not re.fullmatch(r'[a-z0-9](?:[a-z0-9-]*[a-z0-9])?', container):
                            raise InvalidArchiveConfig()
                        declared.append((name, kind, container))
                if not declared or len(declared) > 4 or len({c[2] for c in declared}) != len(declared):
                    raise InvalidArchiveConfig()
                expected.extend(declared)
        _common.safe_write(projected, projection)
        collector.runner(f'{job}/pods.json', projected, 'pod_diagnostics')
        if not skip_logs:
            for name, kind, container in expected:
                leaf = f'{name}.{kind}.{container}.log'
                # runner records a missing source as a transfer gap; an empty file
                # remains valid because the collector permits successful empty logs.
                collector.runner(f'{job}/{leaf}', folder / leaf, 'pod_log')
        return value['items']
    except (OSError, InvalidArchiveConfig, KeyError, TypeError):
        collector.gap('monitoring_incomplete', 'pod identities or declarations unavailable', unit=job)
        return []
    finally:
        projected.unlink(missing_ok=True)
        status_projection.unlink(missing_ok=True)


def project_phase_status(status):
    if not isinstance(status, dict) or not isinstance(status.get('modes'), list) or len(status['modes']) > 1024:
        raise InvalidArchiveConfig()
    if not safe_name(status.get('run_id')) or not safe_name(status.get('phase')):
        raise InvalidArchiveConfig()
    modes = []
    for record in status['modes']:
        if not isinstance(record, dict) or not safe_name(record.get('job')) or not safe_name(record.get('mode')):
            raise InvalidArchiveConfig()
        outcome = record.get('outcome')
        if outcome not in ('not_attempted', 'render_failed', 'apply_failed', 'unknown', 'collect_error', 'complete', 'failed'):
            raise InvalidArchiveConfig()
        repeat = record.get('repeat')
        if repeat is not None and (type(repeat) is not int or repeat < 1): raise InvalidArchiveConfig()
        row = {'job': record['job'], 'mode': record['mode'], 'repeat': repeat, 'outcome': outcome}
        for key in ('render', 'apply', 'wait', 'collect', 'delete', 'confirm'):
            value = record.get(key)
            if value is not None and type(value) is not int: raise InvalidArchiveConfig()
            row[key] = value
        if 'collect_gap' in record:
            if type(record['collect_gap']) is not bool: raise InvalidArchiveConfig()
            row['collect_gap'] = record['collect_gap']
        modes.append(row)
    return _common.encoded({'schema_version': 1, 'kind': 'phase_status_diagnostics', 'run_id': status['run_id'],
                            'phase': status['phase'], 'modes': modes})


def check_v2_actor_logs(collector, phase, status, units, artifacts, private):
    """Bind required stdout evidence and collection success for each V2 actor."""
    from types import SimpleNamespace
    spec = importlib.util.spec_from_file_location('parchwell_archive_probe_v2', Path(__file__).with_name('probe_v2.py'))
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    validator = module.ProbeV2(SimpleNamespace(repo=Path(__file__).resolve().parents[3]))
    limits = validator.limits
    by_job = {record.get('job'): record for record in status['modes'] if isinstance(record, dict)}
    for unit in units:
        if unit['mode'] != 'single-action-v2' or unit['state'] != 'started': continue
        job, pod, uid = unit['job_name'], unit['pod_name'], unit['pod_uid']
        role = None
        try:
            record = by_job[job]
            role, stage = record['role'], record['stage']
            if role not in module.ACCOUNTS or not isinstance(stage, str) or not safe_name(stage) or not pod or not uid:
                raise InvalidArchiveConfig()
            folder = Path(artifacts) / job
            collection = _common.read_json(folder / 'collection-status.json')
            if (not isinstance(collection, dict) or set(collection) != {'complete', 'gaps'}
                    or collection['complete'] is not True or collection['gaps'] != []):
                raise InvalidArchiveConfig()
            fd = os.open(Path(private) / 'probe-v2' / 'raw' / job / f'{pod}.containers.{role}.log', os.O_RDONLY | os.O_NOFOLLOW)
            with os.fdopen(fd, 'rb') as stream:
                log = stream.read(limits['execution']['max_container_log_bytes'] + 1)
            if not log or len(log) > limits['execution']['max_container_log_bytes']:
                raise InvalidArchiveConfig()
            # A successful collection still needs a complete final line.
            if not log.endswith(b'\n'): raise InvalidArchiveConfig()
            lines = log.splitlines(keepends=True)
            indices = []; documents = []
            for marker, capture in ((module.REGISTRATION_MARKER, False), (module.CAPTURE_MARKER, True)):
                matches = [(i, line[len(marker):].removesuffix(b'\n').removesuffix(b'\r'))
                           for i, line in enumerate(lines) if line.startswith(marker)]
                if len(matches) != 1: raise InvalidArchiveConfig()
                index, raw = matches[0]; indices.append(index)
                bound = limits['capture']['MaxCaptureBytes' if capture else 'MaxRegistrationBytes']
                document = module.decode(raw, bound)
                fields = module.IDENTITY_KEYS + (['managed_version', 'native_version', 'repeats', 'handler_exit_code', 'cleanup_completed', 'host_cleanup_completed'] if capture else [])
                expected = {'run_id': phase['run_id'], 'scenario_id': unit['item'], 'configuration_id': record['configuration'],
                            'stage': stage, 'source_commit': phase['source_commit'], 'job_name': job,
                            'pod_name': pod, 'pod_uid': uid, 'service_account': role}
                if (not module.closed(document, fields) or type(document['schema_version']) is not int
                        or document['schema_version'] != 2 or document['mode'] != 'single-action-v2'
                        or document['synthetic'] is not True or any(document[key] != value for key, value in expected.items())
                        or capture and not validator.capture_shape(document)):
                    raise InvalidArchiveConfig()
                documents.append(document)
            if indices[0] >= indices[1]: raise InvalidArchiveConfig()
            import base64, hashlib
            def metadata(value):
                if value is None: return {'is_null': True, 'length': None, 'sha256': None}
                raw = base64.b64decode(value, validate=True)
                return {'is_null': False, 'length': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}
            for slot in documents[1]['repeats']:
                if slot['requested'] is not None:
                    slot['requested']['value_base64'] = metadata(slot['requested']['value_base64'])
                record_value = slot['read_observation']['record']
                if record_value is not None:
                    record_value['value_base64'] = metadata(record_value['value_base64'])
            # Preserve diagnostic lines and their ordering around the two structured
            # records. Recognized body-bearing or credential assignments fail closed.
            diagnostic_lines = []
            for index, line in enumerate(lines):
                if index in indices:
                    diagnostic_lines.append({'record': 'registration' if index == indices[0] else 'capture'})
                    continue
                text = line.decode('utf-8', errors='strict')
                if re.search(r'(?i)(?:[\"\']?value_base64[\"\']?\s*[:=]|\b(?:password|client_secret|access_token|connection_string|sharedaccesskey)\s*[\"\']?\s*[:=]|\bbearer\s+[A-Za-z0-9._~+/=-]+)', text):
                    raise InvalidArchiveConfig()
                diagnostic_lines.append({'text': text})
            name = f'probe-v2/raw/{job}/{pod}.containers.{role}.log'
            refs = {}
            for slot in documents[1]['repeats']:
                for row in slot['statistics']:
                    raw = base64.b64decode(row['raw_base64'], validate=True)
                    refs[(slot['client_id'], row['sequence'])] = (len(raw), hashlib.sha256(raw).hexdigest())
            collector.native_existing[('runner', name)] = refs
            source = {'container': 'runner', 'name': name, 'etag': None, 'bytes': len(log), 'sha256': hashlib.sha256(log).hexdigest()}
            projection = {'schema_version': 1, 'kind': 'source_metadata_projection', 'source': source,
                          'format': 'probe_v2_stdout', 'metadata': {'registration': documents[0], 'capture': documents[1], 'lines': diagnostic_lines}}
            collector.projected_runner(name, _common.encoded(projection))
        except (OSError, ValueError, KeyError, TypeError, AttributeError, _common.Invalid):
            if pod and 'role' in locals() and isinstance(role, str) and safe_name(role):
                collector.unavailable_projection(f'probe-v2/raw/{job}/{pod}.containers.{role}.log')
            else:
                collector.gap('monitoring_incomplete', 'v2 actor stdout or collection incomplete', unit=job)


def collect_ordinary(collector, phase, status, containers, artifacts, job_name):
    run, selected, scenario = phase['run_id'], phase['phase'], phase['scenario']
    diagnostic = phase.get('b10_abrupt_diagnostic') is True
    scientific_b10 = scenario == 'B10' and re.fullmatch(r'b10-[0-9]+-[0-9]+', run) is not None
    slots = (1,) if diagnostic else (1, 2, 3, 4, 5)
    if diagnostic:
        repeat_modes = ('start-line', 'loadgen', 'loadgen-restart', 'loadgen-successor', 'b10-abrupt-verify')
        phase_modes = ()
    elif scientific_b10 and selected == 'C1':
        repeat_modes = ('start-line', 'loadgen', 'loadgen-restart', 'loadgen-successor', 'b10-abrupt-verify')
        phase_modes = ('b10-sweep-line', 'b10-final-sweep', 'judge')
    elif scientific_b10:
        repeat_modes = ('start-line', 'loadgen', 'verify')
        phase_modes = ('b10-sweep-line', 'b10-final-sweep', 'judge')
    elif scenario == 'B10':
        repeat_modes = ('start-line', 'loadgen', 'loadgen2', 'verify')
        phase_modes = ('b10-sweep-line', 'b10-sweep', 'judge')
    else:
        repeat_modes = ('start-line', 'loadgen', 'verify')
        phase_modes = ('judge',)
    planned = [(repeat, mode) for repeat in slots for mode in repeat_modes] + [(None, mode) for mode in phase_modes]
    records = status.get('modes')
    if not isinstance(records, list): raise InvalidArchiveConfig()
    index = {}
    for record in records:
        if not isinstance(record, dict): raise InvalidArchiveConfig()
        key = (record.get('repeat'), record.get('mode'))
        if key not in planned or key in index: raise InvalidArchiveConfig()
        index[key] = record
    units = []
    no_kill_repeats = set()
    first_pods = {}
    for repeat, mode in planned:
        job = job_name(phase, mode, repeat)
        record = index.get((repeat, mode))
        if record is not None and record.get('job') != job: raise InvalidArchiveConfig()
        started = record is not None and record.get('apply') is not None
        optional_no_kill = repeat in no_kill_repeats and mode in ('loadgen-successor', 'b10-abrupt-verify')
        if record is None and not optional_no_kill: collector.gap('monitoring_incomplete', 'missing job status', unit=job)
        elif record is not None and not started and not optional_no_kill and not diagnostic and mode in ('start-line', 'verify', 'b10-abrupt-verify', 'b10-final-sweep', 'judge'):
            collector.gap('monitoring_incomplete', 'required job not started', unit=job)
        pods = read_job_pods(collector, artifacts, job, record) if started else []
        if started and not pods: collector.gap('monitoring_incomplete', 'missing job pod', unit=job)
        unit = {'repeat': repeat, 'item': None, 'mode': mode, 'job_name': job,
                'pod_name': None, 'pod_uid': None, 'instance_id': None,
                'state': 'started' if started else 'not_started'}
        if pods:
            for pod in pods:
                observed = dict(unit, pod_name=pod['name'], pod_uid=pod['uid'])
                if mode.startswith('loadgen'): observed['instance_id'] = pod['name']
                units.append(observed)
        else:
            units.append(unit)
        fixed = []
        root = f'{run}/{selected}/' + (f'r{repeat}/' if repeat is not None else '')
        if mode == 'start-line': fixed = ['start-line.json', 'broker-inventory.json', 'start-line-completion.json']
        elif mode == 'verify': fixed = ['result.json', 'verify-completion.json']
        elif mode == 'b10-abrupt-verify':
            segment = 'b10-abrupt-diagnostic' if diagnostic else 'b10-abrupt'
            fixed = [segment + '/nonfinal-result.json'] + ([] if diagnostic else [segment + '/verify-completion.json'])
        elif mode == 'judge': fixed = ['candidate.json', 'judge-completion.json']
        elif mode == 'b10-sweep-line': fixed = ['sweep-line.json', 'b10-sweep-line-completion.json']
        elif mode == 'b10-sweep': fixed = ['b10-finalization.json', 'b10-sweep-completion.json']
        elif mode == 'b10-final-sweep': fixed = ['b10-final-sweep.json', 'b10-final-sweep-completion.json']
        for leaf in fixed:
            if started: collector.source(containers['verifier'], root + leaf, 'verifier_output', repeat)
            else: collector.not_started(containers['verifier'], root + leaf, 'verifier_output', repeat)
        for pod in pods:
            name, uid = pod['name'], pod['uid']
            if mode in ('loadgen-restart', 'loadgen-successor') and (diagnostic or scientific_b10 and selected == 'C1'):
                owner = job_name.__self__
                verifier = owner.b10_abrupt() if diagnostic else owner.b10_abrupt(repeat, collector.transfer.deadline, [0])
                observation = {'job_name': job, 'pod_name': name, 'pod_uid': uid}
                validator = manifest_validator = successor_validator = registration_validator = None
                if mode == 'loadgen-restart':
                    first_pods[repeat] = name
                    validator = lambda raw, verifier=verifier, observation=observation: verifier.envelope(raw, 'termination', observation)
                    manifest_validator = lambda raw, verifier=verifier, observation=observation: verifier.envelope(raw, 'abrupt_manifest', observation)
                    registration_validator = lambda raw, verifier=verifier, observation=observation: verifier.envelope(raw, 'registration', observation)
                else:
                    predecessor = first_pods.get(repeat)
                    successor_validator = lambda raw, verifier=verifier, observation=observation, predecessor=predecessor: verifier.envelope(raw, 'successor_receipt', observation, predecessor)
                    registration_validator = lambda raw, verifier=verifier, observation=observation, predecessor=predecessor: verifier.envelope(raw, 'registration', observation, predecessor)
                no_kill = collector.abrupt(containers['ledger'], run, repeat, name,
                                           'first' if mode == 'loadgen-restart' else 'successor', diagnostic, validator,
                                           manifest_validator, successor_validator, first_pods.get(repeat), registration_validator)
                terminated = [entry for entry in pod['containers'] if entry['kind'] == 'containerStatuses'
                              and entry['name'] == 'loadgen' and type(entry['terminated_exit_code']) is int]
                if no_kill and record['outcome'] == 'complete' and len(terminated) == 1:
                    no_kill_repeats.add(repeat)
            elif mode in ('loadgen', 'loadgen2'):
                collector.producer(containers['ledger'], run, selected, repeat, job, name, uid, phase['source_commit'])
                for leaf in ('ledger.jsonl', 'manifest.json', 'statistics.jsonl', 'request-lifecycle.jsonl', 'capabilities.json'):
                    collector.optional_source(containers['ledger'], f'{root}producer/{name}/diagnostics/{leaf}', 'producer_diagnostic', repeat)
            elif mode in ('verify', 'b10-abrupt-verify', 'b10-sweep', 'b10-final-sweep'):
                indices = range(10) if mode == 'b10-sweep' else range(1)
                for index_value in indices:
                    owning_repeat = index_value // 2 + 1 if mode == 'b10-sweep' else repeat
                    capture_id, capture_prefix = capture_name(run, selected, mode, job, uid, index_value, owning_repeat)
                    identity = {'run_id': run, 'phase': selected, 'scenario_id': scenario,
                                'configuration_id': phase['configuration'], 'job_name': job,
                                'pod_uid': uid, 'source_commit': phase['source_commit']}
                    collector.capture(containers['verifier'], capture_prefix, identity, mode, owning_repeat, capture_id)
    return units


NATIVE_MONITORING_MODES = frozenset((
    'loadgen', 'loadgen2', 'loadgen-restart', 'loadgen-successor',
    'single-action', 'single-action-seed', 'single-action-v2', 'idle-observation',
    'consumer-verify', 'start-line', 'verify', 'b10-sweep-line', 'b10-sweep',
    'b10-final-sweep', 'b10-abrupt-verify',
))


def collect_native_monitoring(collector, phase, status, units):
    """Read native-host indexes from recorded starts; never list storage."""
    spec = importlib.util.spec_from_file_location('parchwell_archive_native', Path(__file__).with_name('archive_native_monitoring.py'))
    native = importlib.util.module_from_spec(spec); spec.loader.exec_module(native)
    by_job = {r['job']: r for r in status['modes'] if isinstance(r, dict) and isinstance(r.get('job'), str)}
    for unit in units:
        if (unit['state'] != 'started' or not unit['pod_uid']
                or unit['mode'] not in NATIVE_MONITORING_MODES
                or phase['scenario'] == 'probe-batch' and phase.get('run_kind') == 'smoke'):
            continue
        job = unit['job_name']
        record = by_job.get(job, {})
        scenario = unit['item'] or phase['scenario']
        configuration = record.get('configuration') or phase.get('configuration') or phase['phase']
        run = phase['run_id']
        selected = phase['phase']
        if phase['scenario'] in ('probe-batch', 'probe-batch-2', 'idle-observation', 'ledger-batch'):
            selected = configuration
        if phase['scenario'] == 'ledger-batch' and unit['item']:
            run = f"{run}-{unit['item'].lower()}"
        identity = {'run_id': run, 'phase': selected, 'scenario_id': scenario,
                    'configuration_id': configuration, 'job_name': job, 'pod_uid': unit['pod_uid'],
                    'source_commit': phase['source_commit']}
        prefix = f"{run}/{selected}/{job}/{unit['pod_uid']}/native-monitoring/"
        documents = []
        def inspect(path, size):
            try:
                with open(path, 'rb') as stream:
                    data = stream.read(native.MAX_INDEX + 1)
                documents.append(native.index(data, identity, prefix))
            except (OSError, ValueError, TypeError, UnicodeError, RecursionError):
                raise Gap('monitoring_incomplete', 'native index contract') from None
        obj = collector.source('native-monitoring', prefix + 'index.json', 'native_monitoring_index',
                               repeat=unit['repeat'], inspect_source=inspect, max_object=native.MAX_INDEX)
        if obj is None or not documents:
            collector.gap('monitoring_incomplete', 'native index unavailable', unit=job)
            continue
        document = documents[0]
        if document['diagnostics_truncated']:
            collector.gap('diagnostics_truncated', 'native index diagnostics', unit=job)
        if not document['capture_complete']:
            collector.gap('monitoring_incomplete', 'native capture incomplete', unit=job)
        for client in document['clients']:
            for descriptor in client['raw_sources']:
                if descriptor['kind'] == 'existing_capture':
                    parts = descriptor['source'].split('/', 1)
                    existing = next((item for item in collector.objects
                                     if len(parts) == 2 and item['source']['container'] == parts[0]
                                     and item['source']['name'] == parts[1] and item['state'] == 'verified'), None)
                    refs = collector.native_existing.get(tuple(parts), {})
                    actual = refs.get((client['client_id'], descriptor['report_ordinal']))
                    pod = unit['pod_name']
                    same_pod = len(parts) == 2 and bool(pod) and (
                        parts[0] == 'runner' and parts[1].startswith(f'probe-v2/raw/{job}/{pod}.containers.')
                        or parts[0] in ('ledger', 'single-action') and (
                            f'/producer/{pod}/' in '/' + parts[1]
                            or f'/b10-abrupt/{pod}/' in '/' + parts[1]
                            or f'/b10-abrupt-diagnostic/{pod}/' in '/' + parts[1]
                            or parts[1].endswith(f'/instances/{pod}/capture.json')))
                    if existing is None or not same_pod or actual != (descriptor['byte_length'], descriptor['sha256']):
                        collector.gap('monitoring_incomplete', 'native existing capture binding', unit=job)
                    continue
                def inspect_chunk(path, size, descriptor=descriptor, ordinal=client['ordinal']):
                    try:
                        native.chunk(path, descriptor, ordinal, collector.transfer.deadline)
                    except TimeoutError:
                        raise Gap('deadline', 'native chunk validation') from None
                    except (OSError, ValueError, TypeError, UnicodeError, RecursionError):
                        raise Gap('monitoring_incomplete', 'native chunk contract') from None
                result = collector.source('native-monitoring', descriptor['source'], 'native_monitoring_raw',
                                          repeat=unit['repeat'], expected_length=descriptor['byte_length'],
                                          expected_sha256=descriptor['sha256'], max_object=native.MAX_RAW,
                                          inspect_source=inspect_chunk)
                if result is None:
                    collector.gap('monitoring_incomplete', 'native raw unavailable', unit=job)


def manifest_run(phase, identity):
    """Use the frozen phase identifier for batch-wide configuration identity."""
    scenario = phase.get('scenario')
    allowed = {'A01': ('probe', 'C0', 'C3'), 'B04': ('C0', 'C1'), 'B10': ('C0', 'C1'),
               'probe-batch': ('single-action',), 'probe-batch-2': ('single-action',),
               'idle-observation': ('idle-observation',), 'ledger-batch': ('ledger-batch',)}
    if scenario not in allowed or phase.get('phase') not in allowed[scenario] or phase['phase'] != identity['phase']:
        raise InvalidArchiveConfig()
    diagnostic = phase.get('b10_abrupt_diagnostic') is True
    if diagnostic and (scenario != 'B10' or phase['phase'] != 'C1' or phase.get('purpose') != 'diagnostic_trial' or phase.get('run_kind') != 'smoke'):
        raise InvalidArchiveConfig()
    prefix = ('b10d' if diagnostic else {'A01': 'a01', 'B04': 'b04', 'B10': 'b10', 'probe-batch': 'probe',
              'probe-batch-2': 'probe', 'idle-observation': 'idle', 'ledger-batch': 'ledger'}[scenario])
    if identity['run_id'] != f"{prefix}-{identity['github_run_id']}-{identity['github_run_attempt']}":
        raise InvalidArchiveConfig()
    configuration = phase.get('configuration')
    batch = scenario in ('probe-batch', 'probe-batch-2', 'idle-observation', 'ledger-batch')
    if not batch and configuration != ('C0' if phase['phase'] == 'probe' else phase['phase']):
        raise InvalidArchiveConfig()
    if configuration is None and batch:
        configuration = phase['phase']
    if not isinstance(configuration, str) or not configuration.isascii() or not safe_name(configuration):
        raise InvalidArchiveConfig()
    images = phase.get('images', {})
    if not isinstance(images, dict): raise InvalidArchiveConfig()
    values = {}
    for worker in ('loadgen', 'verifier', 'probe'):
        entry = images.get(worker)
        if entry is not None:
            if not isinstance(entry, dict) or set(entry) != {'reference', 'digest'}:
                raise InvalidArchiveConfig()
            reference, digest = entry['reference'], entry['digest']
            if (not isinstance(digest, str) or not re.fullmatch(r'sha256:[0-9a-f]{64}', digest)
                    or not isinstance(reference, str) or len(reference) > 1024 or not reference.isascii()
                    or not re.fullmatch(r'[a-z0-9.-]+/parchwell/' + worker + r'@sha256:[0-9a-f]{64}', reference)
                    or not reference.endswith('@' + digest)):
                raise InvalidArchiveConfig()
        else:
            reference = None
        values[worker] = reference
    run_kind = phase.get('run_kind', 'scientific')
    purpose = phase.get('purpose', 'scientific')
    if run_kind not in ('smoke', 'batch', 'scientific') or not isinstance(purpose, str): raise InvalidArchiveConfig()
    admissible = run_kind != 'smoke' and phase.get('purpose') != 'diagnostic_trial' and phase['phase'] != 'probe'
    return {**identity, 'scenario_id': phase['scenario'], 'configuration_id': configuration, 'images': values,
            'run_kind': run_kind, 'purpose': purpose, 'scientific_admissible': admissible}


def record_archive(phase_instance, receipt):
    validate_receipt(receipt)
    state = phase_instance.state()
    if not isinstance(state, dict): raise InvalidArchiveConfig()
    state['archive'] = {'archive_complete': receipt['archive_complete'], 'manifest': receipt['manifest'],
                        'completion': receipt['completion'], 'gaps': receipt['gaps']}
    phase_instance.save_state(state)


def try_record_archive(phase_instance, receipt):
    try:
        record_archive(phase_instance, receipt)
        return True
    except Exception:
        return False


def local_failure(public, manifest, identity, code, boundary):
    gap = {'code': code, 'boundary': boundary, 'object_name': None, 'expected_unit': None}
    manifest.update(archive_complete=False, gaps=[gap], completion=None)
    _common.safe_write(public / 'archive-manifest.json', _common.encoded(manifest))
    receipt = {'schema_version': 1, 'kind': 'run_evidence_archive_receipt', 'identity': identity,
               'archive_complete': False, 'manifest': None, 'completion': None, 'gaps': [gap]}
    validate_receipt(receipt)
    write_receipt(public, receipt)
    return receipt


def execute(phase_instance, environ, deadline=None):
    if deadline is None:
        deadline = time.monotonic() + ArchiveDeadlineSeconds
    validated = False
    public = manifest = identity = None
    try:
        private, artifacts, phase, identity, config, source_host, containers = load_context(environ)
        run = manifest_run(phase, identity)
        validated = True
        prefix = 'v1/{}/{}/{}/{}/{}/'.format(identity['source_commit'], identity['github_run_id'],
                                               identity['github_run_attempt'], identity['run_id'], identity['phase'])
        spool, public = private / 'archive', artifacts / 'archive'
        for directory in (spool, public):
            directory.mkdir(mode=0o700, exist_ok=True)
            if directory.is_symlink() or not directory.is_dir(): raise InvalidArchiveConfig()
        manifest = {'schema_version': 1, 'kind': 'run_evidence_archive', 'archive_complete': False,
                    'run': run, 'destination': {'account_host': config['account_name'] + '.blob.core.windows.net',
                    'container': 'run-evidence', 'prefix': prefix, 'retention_days': 90}, 'policy': dict(_common.POLICY),
                    'expected_units': [], 'objects': [], 'captures': [],
                    'totals': {'expected_objects': 0, 'verified_objects': 0, 'verified_bytes': 0}, 'gaps': [], 'completion': None}
        if source_host is None:
            try_record_archive(phase_instance, local_failure(public, manifest, identity, 'transport', 'ephemeral output unavailable'))
            return 1
        try:
            _common.preflight(deadline)
        except _common.Invalid:
            try_record_archive(phase_instance, local_failure(public, manifest, identity, 'config_invalid', 'archive transport version'))
            return 2
        except Gap as error:
            try_record_archive(phase_instance, local_failure(public, manifest, identity, error.code, error.boundary))
            return 1
        except KeyboardInterrupt:
            try_record_archive(phase_instance, local_failure(public, manifest, identity, 'interrupted', 'archive preflight'))
            return 1
        transfer = Transfer(source_host.split('.')[0], config['account_name'], spool, deadline)
        collector = Collector(transfer, prefix)
        if run['images']['loadgen'] is None or run['images']['verifier'] is None:
            collector.gap('monitoring_incomplete', 'image identity unavailable')
        try:
            status = _common.read_json(artifacts / 'phase-status.json')
            if not isinstance(status, dict) or status.get('run_id') != identity['run_id'] or status.get('phase') != identity['phase']:
                raise InvalidArchiveConfig()
            projected_status = spool / 'phase-status-projection.json'
            try:
                _common.safe_write(projected_status, project_phase_status(status))
                collector.runner('phase-status.json', projected_status, 'phase_status')
            finally:
                projected_status.unlink(missing_ok=True)
            if phase['scenario'] in ('probe-batch', 'probe-batch-2', 'idle-observation', 'ledger-batch'):
                spec = importlib.util.spec_from_file_location('parchwell_archive_nonledger', Path(__file__).with_name('archive_nonledger.py'))
                module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
                units = module.collect(collector, phase, status, containers, artifacts)
                if phase['scenario'] == 'probe-batch-2':
                    check_v2_actor_logs(collector, phase, status, units, artifacts, private)
                for job in sorted({unit['job_name'] for unit in units if unit['state'] == 'started'}):
                    read_job_pods(collector, artifacts, job, next(record for record in status['modes'] if record.get('job') == job),
                                  skip_logs=any(unit['job_name'] == job and unit['mode'] == 'single-action-v2' for unit in units),
                                  v2=phase['scenario'] == 'probe-batch-2')
            else:
                units = collect_ordinary(collector, phase, status, containers, artifacts, phase_instance.mode_job)
            collect_native_monitoring(collector, phase, status, units)
            # Cross-check the adapters against the independently derived fixed paths;
            # this never discovers objects by listing or starts a second transfer.
            if phase['scenario'] in ('probe-batch', 'probe-batch-2', 'idle-observation', 'ledger-batch'):
                fixed, _ = fixed_nonledger_objects(phase, status['modes'])
            elif phase['scenario'] != 'B10':
                fixed = ordinary_fixed_inventory(phase['run_id'], phase['phase'], status['modes'])
                expected_units, _ = ordinary_units(phase['run_id'], phase['phase'], status['modes'])
                if {(u['job_name'], u['state']) for u in expected_units} != {(u['job_name'], u['state']) for u in units}:
                    collector.gap('monitoring_incomplete', 'expected unit inventory')
            else:
                fixed = []
            found = {(obj['source']['container'], obj['source']['name']): obj for obj in collector.objects}
            for expected in fixed:
                obj = found.get((containers[expected['container']], expected['name']))
                if obj is None or obj['required'] != expected['required']:
                    collector.gap('monitoring_incomplete', 'fixed object inventory', expected['name'])
            manifest['expected_units'] = sorted(units, key=lambda unit: (unit['job_name'], unit['repeat'] or 0, unit['pod_name'] or '', unit['pod_uid'] or ''))
        except (OSError, ValueError, KeyError, TypeError, _common.Invalid):
            collector.gap('monitoring_incomplete', 'inventory construction')
        except KeyboardInterrupt:
            collector.gap('interrupted', 'inventory interrupted')
        manifest['objects'] = sorted(collector.objects, key=lambda obj: obj['destination_name'])
        manifest['captures'] = sorted(collector.captures, key=lambda capture: capture['capture_id'])
        manifest['gaps'] = sorted(collector.gaps, key=lambda gap: (gap['code'], gap['boundary'], gap['object_name'] or '', gap['expected_unit'] or ''))
        manifest['totals'] = collector.totals()
        manifest['archive_complete'] = not manifest['gaps'] and all(capture['complete'] for capture in manifest['captures'])
        if len(_common.encoded(manifest)) > 1048576:
            manifest.update(archive_complete=False, expected_units=[], objects=[], captures=[],
                            totals={'expected_objects': 0, 'verified_objects': 0, 'verified_bytes': 0},
                            gaps=[{'code': 'manifest_too_large', 'boundary': 'canonical manifest', 'object_name': None, 'expected_unit': None}])
        receipt = publish_controls(transfer, manifest, identity, public, prefix)
        recorded = try_record_archive(phase_instance, receipt)
        return 0 if receipt['archive_complete'] and recorded else 1
    except (Exception, KeyboardInterrupt) as error:
        if not validated:
            return 2
        if public is not None and manifest is not None and public.is_dir():
            gap = {'code': 'interrupted' if isinstance(error, KeyboardInterrupt) else 'transport',
                   'boundary': 'archive execution', 'object_name': None, 'expected_unit': None}
            try:
                if not (public / 'archive-manifest.json').exists():
                    manifest.update(archive_complete=False, expected_units=[], objects=[], captures=[],
                                    totals={'expected_objects': 0, 'verified_objects': 0, 'verified_bytes': 0},
                                    gaps=[gap], completion=None)
                    _common.safe_write(public / 'archive-manifest.json', _common.encoded(manifest))
                if not (public / 'archive-receipt.json').exists():
                    receipt = {'schema_version': 1, 'kind': 'run_evidence_archive_receipt', 'identity': identity,
                               'archive_complete': False, 'manifest': None, 'completion': None, 'gaps': [gap]}
                    _common.safe_write(public / 'archive-receipt.json', _common.encoded(receipt))
                else:
                    receipt = _common.read_json(public / 'archive-receipt.json')
                record_archive(phase_instance, receipt)
            except (OSError, InvalidArchiveConfig, _common.Invalid, ValueError, KeyError, TypeError):
                pass
        return 1


def legacy_integer(value, bits=64, nonnegative=False):
    return (type(value) is int and -(1 << (bits - 1)) <= value < (1 << (bits - 1))
            and (not nonnegative or value >= 0))


def legacy_error(value, callback=False):
    fields = {'code', 'is_fatal', 'origin', 'reason'} | ({'observed_elapsed_ms'} if callback else set())
    return (isinstance(value, dict) and set(value) == fields
            and legacy_integer(value['code'], 32) and type(value['is_fatal']) is bool
            and value['origin'] in ('construction', 'local', 'broker')
            and value['reason'] == ('error_reason',)
            and (not callback or legacy_integer(value['observed_elapsed_ms'], nonnegative=True)))


def legacy_produce(value):
    if value is None: return True
    fields = {'attempt', 'key', 'started_elapsed_ms', 'terminal', 'result', 'error', 'terminal_elapsed_ms', 'terminal_unix_ms'}
    return (isinstance(value, dict) and set(value) == fields
            and legacy_integer(value['attempt'], 32) and isinstance(value['key'], str)
            and legacy_integer(value['started_elapsed_ms'], nonnegative=True) and type(value['terminal']) is bool
            and (value['result'] is None or value['result'] in ('success', 'error', 'no_outcome'))
            and (value['error'] is None or legacy_error(value['error']))
            and all(value[key] is None or legacy_integer(value[key]) for key in ('terminal_elapsed_ms', 'terminal_unix_ms')))


def legacy_repeat_shape(repeat, mode):
    settings = repeat['settings']; omitted = repeat['omitted_keys']
    errors = repeat['client_errors']; statistics = repeat['statistics']; diagnostics = repeat['diagnostics']
    if (settings != ('settings',) or omitted != ('omitted_keys',)
            or not isinstance(errors, list) or len(errors) > 64 or any(not legacy_error(v, True) for v in errors)
            or not legacy_integer(repeat['client_errors_dropped'], nonnegative=True)
            or not isinstance(statistics, list) or len(statistics) > (80 if mode == 'idle-observation' else 64)
            or not isinstance(diagnostics, list) or len(diagnostics) > (20 if mode == 'idle-observation' else 100)
            or any(not isinstance(v, str) or len(v.encode('utf-8')) > 2048 for v in diagnostics)):
        return False
    for row in statistics:
        if (not isinstance(row, dict) or set(row) != {'sequence', 'observed_elapsed_ms', 'raw_base64'}
                or not legacy_integer(row['sequence'], 32) or row['sequence'] < 1
                or not legacy_integer(row['observed_elapsed_ms'], nonnegative=True)
                or not isinstance(row['raw_base64'], tuple) or len(row['raw_base64']) != 3
                or row['raw_base64'][0] != 'statistics_base64'):
            return False
    if mode == 'idle-observation':
        return (legacy_integer(repeat['client_started_unix_ms'])
                and all(repeat[key] is None or legacy_integer(repeat[key]) for key in ('idle_started_elapsed_ms', 'idle_ended_elapsed_ms'))
                and legacy_produce(repeat['first']) and legacy_produce(repeat['second']))
    return (isinstance(repeat['action'], str) and type(repeat['terminal']) is bool
            and legacy_integer(repeat['elapsed_ms'], nonnegative=True)
            and (repeat['terminal_unix_ms'] is None or legacy_integer(repeat['terminal_unix_ms']))
            and (repeat['result'] is None or repeat['result'] in ('success', 'error', 'no_outcome'))
            and (repeat['error'] is None or legacy_error(repeat['error'])))


def legacy_closed(document, kind, mode):
    common = {'schema_version', 'mode', 'identity', 'job_name', 'pod_name', 'pod_uid', 'source_commit'}
    fields = common | ({'repeats_attempted', 'handler_exit_code', 'cleanup_completed'} if kind == 'completion' else set())
    if kind == 'capture':
        fields |= {'synthetic', 'managed_version', 'native_version', 'repeats'}
        if mode == 'idle-observation':
            fields |= {'run_kind', 'topic', 'partition', 'payload_bytes', 'idle_seconds', 'statistics_interval_ms', 'max_statistics_reports'}
    if (kind not in ('registration', 'completion', 'capture') or mode not in ('single-action', 'idle-observation')
            or not isinstance(document, dict) or set(document) != fields
            or not legacy_integer(document['schema_version'], 32) or not isinstance(document['mode'], str)
            or not isinstance(document['identity'], dict)
            or set(document['identity']) != {'scenario_id', 'configuration_id', 'run_id', 'instance_id'}
            or any(not isinstance(value, str) for value in document['identity'].values())
            or any(not isinstance(document[key], str) for key in ('job_name', 'pod_name', 'pod_uid', 'source_commit'))):
        return False
    if kind == 'completion':
        return (legacy_integer(document['repeats_attempted'], 32) and legacy_integer(document['handler_exit_code'], 32)
                and type(document['cleanup_completed']) is bool)
    if kind == 'capture':
        if (type(document['synthetic']) is not bool or not isinstance(document['managed_version'], str)
                or not isinstance(document['native_version'], str) or not isinstance(document['repeats'], list)):
            return False
        if mode == 'idle-observation':
            if not isinstance(document['run_kind'], str) or not isinstance(document['topic'], str) or any(not legacy_integer(document[key], 32) for key in ('partition', 'payload_bytes', 'idle_seconds', 'statistics_interval_ms', 'max_statistics_reports')):
                return False
            repeat_fields = {'repeat', 'client_id', 'settings', 'omitted_keys', 'client_started_unix_ms', 'first',
                             'idle_started_elapsed_ms', 'idle_ended_elapsed_ms', 'second', 'client_errors',
                             'client_errors_dropped', 'statistics', 'diagnostics'}
        else:
            repeat_fields = {'repeat', 'client_id', 'action', 'terminal', 'elapsed_ms', 'terminal_unix_ms', 'settings',
                             'omitted_keys', 'result', 'error', 'matched', 'client_errors', 'client_errors_dropped',
                             'statistics', 'diagnostics'}
        for repeat in document['repeats']:
            if (not isinstance(repeat, dict) or set(repeat) != repeat_fields
                    or not legacy_integer(repeat['repeat'], 32) or not isinstance(repeat['client_id'], str)
                    or not legacy_repeat_shape(repeat, mode)):
                return False
            if mode == 'single-action' and repeat['matched'] is not None:
                matched = repeat['matched']
                if (not isinstance(matched, dict) or set(matched) != {'topic', 'partition', 'key_base64', 'value_base64'}
                        or not isinstance(matched['topic'], str) or not legacy_integer(matched['partition'], 32)
                        or matched['partition'] < 0 or not isinstance(matched['key_base64'], str)
                        or not isinstance(matched['value_base64'], dict)):
                    return False
                try:
                    import base64
                    base64.b64decode(matched['key_base64'], validate=True)
                except (ValueError, TypeError):
                    return False
        return mode != 'single-action' or sum(len(r['diagnostics']) for r in document['repeats']) <= 100
    return True


def validate_capture_projection(document, mode, identity, run_kind):
    if not legacy_closed(document, 'capture', mode):
        raise Gap('monitoring_incomplete', 'source publication shape')
    return validate_legacy_document(document, 'capture', mode, identity, run_kind)


def validate_legacy_document(document, kind, mode, identity, run_kind):
    """Validate closed publication envelopes and their job/pod/run bindings."""
    common = {'schema_version', 'mode', 'identity', 'job_name', 'pod_name', 'pod_uid', 'source_commit'}
    fields = common | ({'repeats_attempted', 'handler_exit_code', 'cleanup_completed'} if kind == 'completion' else set())
    if kind == 'capture':
        fields |= {'synthetic', 'managed_version', 'native_version', 'repeats'}
        if mode == 'idle-observation':
            fields |= {'run_kind', 'topic', 'partition', 'payload_bytes', 'idle_seconds', 'statistics_interval_ms', 'max_statistics_reports'}
    if (not legacy_closed(document, kind, mode) or not isinstance(document, dict) or set(document) != fields or type(document['schema_version']) is not int
            or document['schema_version'] != 1 or document['mode'] != mode
            or document['identity'] != {'scenario_id': identity['scenario_id'], 'configuration_id': identity['configuration_id'],
                                        'run_id': identity['run_id'], 'instance_id': identity['pod_name']}
            or any(document[key] != identity[key] for key in ('job_name', 'pod_name', 'pod_uid', 'source_commit'))):
        return False
    if kind == 'completion':
        return (type(document['repeats_attempted']) is int and document['repeats_attempted'] == 5
                and type(document['handler_exit_code']) is int and document['handler_exit_code'] == 0
                and document['cleanup_completed'] is True)
    if kind == 'capture':
        if (document['synthetic'] is not True or not isinstance(document['managed_version'], str) or not document['managed_version']
                or not isinstance(document['native_version'], str) or not document['native_version']
                or not isinstance(document['repeats'], list) or len(document['repeats']) != 5):
            return False
        if mode == 'idle-observation':
            if any(document[key] != expected or type(document[key]) is not type(expected) for key, expected in
                   {'run_kind': run_kind, 'topic': 'document-events', 'partition': 0, 'payload_bytes': 1000,
                    'idle_seconds': 420, 'statistics_interval_ms': 15000, 'max_statistics_reports': 80}.items()):
                return False
            repeat_fields = {'repeat', 'client_id', 'settings', 'omitted_keys', 'client_started_unix_ms', 'first',
                             'idle_started_elapsed_ms', 'idle_ended_elapsed_ms', 'second', 'client_errors',
                             'client_errors_dropped', 'statistics', 'diagnostics'}
        else:
            repeat_fields = {'repeat', 'client_id', 'action', 'terminal', 'elapsed_ms', 'terminal_unix_ms', 'settings',
                             'omitted_keys', 'result', 'error', 'matched', 'client_errors', 'client_errors_dropped',
                             'statistics', 'diagnostics'}
        for index, repeat in enumerate(document['repeats'], 1):
            if (not isinstance(repeat, dict) or set(repeat) != repeat_fields or type(repeat['repeat']) is not int
                    or repeat['repeat'] != index or repeat['client_id'] != f"{identity['pod_name']}-r{index}"):
                return False
    return True

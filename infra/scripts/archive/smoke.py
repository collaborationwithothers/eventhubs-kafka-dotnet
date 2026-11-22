#!/usr/bin/env python3
"""Bounded synthetic archive readiness probe; no scientific evidence is produced."""
import contextlib
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import resource
import selectors
import shutil
import signal
import stat
import subprocess
import sys
import time
from urllib.parse import urlsplit

MIB = 1048576
MAX_OBJECT = 256 * MIB
DEADLINE = 1800
FIXTURE = b'Parchwell archive readiness synthetic fixture v1\n'
POLICY = {'max_bytes': 17179869184, 'max_object_bytes': MAX_OBJECT,
          'max_objects': 1024, 'deadline_seconds': 1800, 'transfer_passes': 1,
          'metadata_chunk_bytes': 134217728, 'metadata_row_bytes': MIB}

class Invalid(Exception):
    pass

class Gap(Exception):
    def __init__(self, code, boundary):
        self.code, self.boundary = code, boundary


def pairs(items):
    result = {}
    for key, value in items:
        if key in result:
            raise Invalid()
        result[key] = value
    return result


def read_json(path):
    try:
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
        with os.fdopen(fd, 'rb') as f:
            if not stat.S_ISREG(os.fstat(f.fileno()).st_mode):
                raise Invalid()
            data = f.read(4 * MIB + 1)
        if len(data) > 4 * MIB:
            raise Invalid()
        value = json.loads(data, object_pairs_hook=pairs, parse_constant=lambda _: (_ for _ in ()).throw(Invalid()))
        def depth(item, level):
            if level > 32: raise Invalid()
            if isinstance(item, dict):
                for child in item.values(): depth(child, level + 1)
            elif isinstance(item, list):
                for child in item: depth(child, level + 1)
        depth(value, 1)
        return value
    except (OSError, ValueError, RecursionError):
        raise Invalid() from None


def unwrap(value):
    if not isinstance(value, dict) or 'value' not in value:
        raise Invalid()
    return value['value']


def identifier(value, pattern, limit=1024):
    if not isinstance(value, str) or len(value) > limit or not re.fullmatch(pattern, value):
        raise Invalid()
    return value


def endpoint(url):
    if not isinstance(url, str):
        raise Invalid()
    parsed = urlsplit(url)
    if (parsed.scheme != 'https' or parsed.path not in ('', '/') or parsed.query or parsed.fragment
            or parsed.username is not None or parsed.password is not None or parsed.port is not None
            or not re.fullmatch(r'[a-z0-9]{3,24}\.blob\.core\.windows\.net', parsed.netloc)):
        raise Invalid()
    return parsed.netloc


def config(include_source):
    try:
        private = Path(os.environ['PARCHWELL_PRIVATE_DIRECTORY'])
        artifacts = Path(os.environ['PARCHWELL_ARTIFACT_DIRECTORY'])
        if not private.is_absolute() or not artifacts.is_absolute() or not private.is_dir() or not artifacts.is_dir():
            raise Invalid()
        private, artifacts = private.resolve(), artifacts.resolve()
        if private == artifacts or private in artifacts.parents or artifacts in private.parents:
            raise Invalid()
        phase = read_json(private / 'phase.json')
        persistent = read_json(private / 'persistent-output.json')
        archive = unwrap(persistent['evidence_archive'])
        fields = {'storage_account_id', 'account_name', 'blob_service_uri', 'container_name', 'container_id', 'retention_days'}
        if not isinstance(archive, dict) or set(archive) != fields:
            raise Invalid()
        account = identifier(archive['account_name'], r'[a-z0-9]{3,24}')
        host = endpoint(archive['blob_service_uri'])
        if host != account + '.blob.core.windows.net' or archive['container_name'] != 'run-evidence' or type(archive['retention_days']) is not int or archive['retention_days'] != 90:
            raise Invalid()
        rid = identifier(archive['storage_account_id'], r'/subscriptions/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/resourceGroups/[A-Za-z0-9_.()\-]+/providers/Microsoft\.Storage/storageAccounts/[a-z0-9]+')
        if not rid.endswith('/' + account) or archive['container_id'] != rid + '/blobServices/default/containers/run-evidence':
            raise Invalid()
        run_id = identifier(os.environ['PARCHWELL_RUN_ID'], r'[a-z0-9](?:[a-z0-9-]*[a-z0-9])?', 29)
        github = identifier(os.environ['GITHUB_RUN_ID'], r'[1-9][0-9]*', 32)
        attempt = identifier(os.environ['GITHUB_RUN_ATTEMPT'], r'[1-9][0-9]*', 9)
        sha = identifier(os.environ['GITHUB_SHA'], r'[0-9a-f]{40}')
        if not isinstance(phase, dict) or phase['run_id'] != run_id or phase['source_commit'] != sha or not run_id.endswith('-' + github + '-' + attempt):
            raise Invalid()
        scenario = identifier(phase['scenario'], r'[A-Za-z0-9-]+')
        phase_name = identifier(phase['phase'], r'[a-z0-9][a-z0-9-]*')
        configuration = identifier(phase['configuration'], r'[A-Za-z0-9_.-]+')
        if scenario != 'A01' or phase_name != 'probe':
            raise Invalid()
        source = None
        if include_source:
            if not (private / 'ephemeral-output.json').exists():
                raise Gap('transport', 'ephemeral output unavailable')
            blob = unwrap(read_json(private / 'ephemeral-output.json')['blob'])
            source = endpoint(blob['account_url'])
            if source == host or blob['containers']['archive_smoke_source'] != 'archive-smoke-source':
                raise Invalid()
        return private, artifacts, host, source, {'github_run_id': github, 'github_run_attempt': int(attempt), 'run_id': run_id, 'phase': phase_name, 'source_commit': sha}, scenario, configuration
    except (KeyError, TypeError, ValueError, OSError):
        raise Invalid() from None


def limit_child():
    resource.setrlimit(resource.RLIMIT_FSIZE, (MAX_OBJECT, MAX_OBJECT))


def stop_command(proc):
    # The wrapper starts its real CLI in another session and holds its lock
    # while reaping it. Give that handler time to finish before escalation.
    previous = {sig: signal.signal(sig, signal.SIG_IGN) for sig in (signal.SIGINT, signal.SIGTERM)}
    try:
        if proc.poll() is None:
            with contextlib.suppress(ProcessLookupError):
                proc.send_signal(signal.SIGTERM)
        try:
            proc.wait(timeout=5)
            return
        except subprocess.TimeoutExpired:
            pass
        # Linux fallback: stop descendants before stopping the lock holder.
        parents = {proc.pid}; descendants = set()
        for _ in range(32):
            found = set()
            try:
                entries = list(Path('/proc').iterdir())
            except OSError:
                entries = []
            for item in entries:
                if not item.name.isdigit():
                    continue
                try:
                    fields = (item / 'stat').read_text().rsplit(')', 1)[1].split()
                    if int(fields[1]) in parents:
                        found.add(int(item.name))
                except (OSError, ValueError, IndexError):
                    continue
            found -= descendants
            if not found:
                break
            descendants.update(found); parents = found
        for pid in descendants:
            try:
                os.kill(pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
        try:
            proc.wait(timeout=2)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait(timeout=2)
    finally:
        for sig, handler in previous.items():
            signal.signal(sig, handler)


def command(args, deadline, cap=MIB, input_data=None):
    end = min(time.monotonic() + 60, deadline)
    if end <= time.monotonic():
        raise Gap('deadline', 'global deadline')
    pending = []
    previous = {sig: signal.signal(sig, lambda number, _: pending.append(number))
                for sig in (signal.SIGINT, signal.SIGTERM)}
    try:
        proc = subprocess.Popen(args, stdin=subprocess.PIPE if input_data is not None else subprocess.DEVNULL,
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                start_new_session=True, preexec_fn=limit_child)
    finally:
        for sig, handler in previous.items(): signal.signal(sig, handler)
    buffers = {proc.stdout: bytearray(), proc.stderr: bytearray()}
    try:
        if pending: raise KeyboardInterrupt()
        with selectors.DefaultSelector() as selector:
            for stream in buffers:
                selector.register(stream, selectors.EVENT_READ)
            sent = 0
            if input_data is not None:
                os.set_blocking(proc.stdin.fileno(), False)
                selector.register(proc.stdin, selectors.EVENT_WRITE)
            while selector.get_map():
                remaining = end - time.monotonic()
                if remaining <= 0:
                    raise Gap('deadline', 'command timeout')
                for key, _ in selector.select(remaining):
                    if key.fileobj is proc.stdin:
                        try:
                            sent += os.write(key.fd, input_data[sent:sent + 4096])
                        except BrokenPipeError:
                            sent = len(input_data)
                        if sent == len(input_data):
                            selector.unregister(proc.stdin); proc.stdin.close()
                        continue
                    chunk = os.read(key.fd, min(65536, cap + 1 - len(buffers[key.fileobj])))
                    if not chunk:
                        selector.unregister(key.fileobj)
                    else:
                        buffers[key.fileobj].extend(chunk)
                        if len(buffers[key.fileobj]) > cap:
                            raise Gap('transport', 'command output limit')
            remaining = end - time.monotonic()
            if remaining <= 0:
                raise Gap('deadline', 'command timeout')
            proc.wait(timeout=remaining)
        return proc.returncode, bytes(buffers[proc.stdout]), bytes(buffers[proc.stderr])
    except KeyboardInterrupt:
        stop_command(proc)
        error = Gap('interrupted', 'command interrupted')
        error.stderr = bytes(buffers[proc.stderr])
        raise error from None
    except Gap as error:
        stop_command(proc)
        error.stderr = bytes(buffers[proc.stderr])
        raise
    except subprocess.TimeoutExpired:
        stop_command(proc)
        error = Gap('deadline', 'command timeout')
        error.stderr = bytes(buffers[proc.stderr])
        raise error from None
    except BaseException:
        stop_command(proc)
        raise
    finally:
        proc.stdout.close(); proc.stderr.close()
        if proc.stdin is not None: proc.stdin.close()


VERSION_PATTERN = re.compile(r'[0-9]+\.[0-9]+\.[0-9]+[0-9A-Za-z.+-]{0,32}')


def readable_version(value):
    if not isinstance(value, str) or not value.isascii() or not VERSION_PATTERN.fullmatch(value):
        raise Invalid()
    return value


def preflight(deadline):
    try:
        code, output, _ = command(['az', 'version', '--output', 'json'], deadline)
        version = json.loads(output, object_pairs_hook=pairs)
        if code or not isinstance(version, dict):
            raise Invalid()
        azure_cli = readable_version(version.get('azure-cli'))
        code, output, _ = command(['az', '--version'], deadline)
        matches = re.findall(rb"(?m)^Python location ['\"]([^'\"\r\n]+)['\"]\s*$", output)
        if code or len(matches) != 1:
            raise Invalid()
        python = os.fsdecode(matches[0])
        if not os.path.isabs(python) or not os.path.isfile(python) or not os.access(python, os.X_OK):
            raise Invalid()
        code, output, _ = command([python, '-c', 'import importlib.metadata; print(importlib.metadata.version("azure-storage-blob"))'], deadline)
        if code:
            raise Invalid()
        azure_storage_blob = readable_version(output.strip().decode('ascii'))
        return {'python': python, 'azure_cli': azure_cli, 'azure_storage_blob': azure_storage_blob}
    except (OSError, ValueError, TypeError):
        raise Invalid() from None


def safe_write(path, data):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'wb') as output:
        output.write(data)


def encoded(value):
    return (json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=True) + '\n').encode()


def check_deadline(deadline, clock):
    if clock() >= deadline:
        raise Gap('deadline', 'global deadline')


def digest(path, expected, max_bytes=MAX_OBJECT, *, deadline=float('inf'), clock=time.monotonic, side='source'):
    check_deadline(deadline, clock)
    if type(expected) is not int or not 0 <= expected <= max_bytes:
        raise Gap(side + '_length_mismatch', 'invalid expected length')
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    h = hashlib.sha256(); total = 0
    with os.fdopen(fd, 'rb') as f:
        st = os.fstat(f.fileno())
        if not stat.S_ISREG(st.st_mode) or st.st_size != expected or expected > max_bytes:
            raise Gap(side + '_length_mismatch', 'file length')
        while True:
            check_deadline(deadline, clock)
            chunk = f.read(min(65536, max_bytes - total + 1))
            if not chunk:
                break
            total += len(chunk)
            if total > expected or total > max_bytes:
                raise Gap(side + '_length_mismatch', 'stream length')
            h.update(chunk)
    if total != expected:
        raise Gap(side + '_length_mismatch', 'stream length')
    check_deadline(deadline, clock)
    return h.hexdigest()


# Only fixed vocabulary is published; debug streams can contain credentials and paths.
SERVICE_CODES = ('BlobNotFound', 'ResourceNotFound', 'BlobAlreadyExists',
                 'ConditionNotMet', 'AuthenticationFailed', 'AuthorizationFailure',
                 'AuthorizationPermissionMismatch', 'AuthorizationResourceTypeMismatch',
                 'AuthorizationServiceMismatch', 'ContainerNotFound', 'AccountIsDisabled',
                 'InvalidAuthenticationInfo', 'InvalidHeaderValue', 'InvalidQueryParameterValue',
                 'InvalidUri', 'InvalidResourceName', 'ServerBusy', 'OperationTimedOut',
                 'InternalError', 'ServiceUnavailable')
EXCEPTION_TYPES = ('FileNotFoundError', 'PermissionError', 'IsADirectoryError',
                   'OSError', 'ValueError', 'TypeError', 'ImportError', 'ModuleNotFoundError',
                   'CLIError', 'AzureError', 'ClientAuthenticationError',
                   'ServiceRequestError', 'ServiceResponseError', 'HttpResponseError',
                   'ResourceNotFoundError', 'ResourceExistsError', 'ResourceModifiedError',
                   'SSLError', 'ConnectionError', 'ConnectTimeout', 'ReadTimeout',
                   'TimeoutError', 'NameResolutionError', 'gaierror')


def public_request_id(value):
    if not isinstance(value, str) or not re.fullmatch(r'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}', value):
        return None
    value = value.lower()
    if value in {os.environ.get(key, '').lower() for key in ('AZURE_CLIENT_ID', 'AZURE_TENANT_ID', 'AZURE_SUBSCRIPTION_ID')}:
        return None
    return value


def command_evidence(code, stderr):
    # Recognize exact bounded tokens only. Never interpolate untrusted text into output.
    def recognized(values):
        return next((value for value in values if re.search(
            rb'(?<![A-Za-z0-9_])' + value.encode() + rb'(?![A-Za-z0-9_])', stderr)), None)
    exception_matches = re.findall(rb'(?m)^(?:[A-Za-z_][A-Za-z0-9_]*\.)*([A-Za-z_][A-Za-z0-9_]*)(?::[^\r\n]*)?\s*$', stderr)
    exception = next((value.decode() for value in reversed(exception_matches)
                      if value.decode() in EXCEPTION_TYPES), None)
    status_matches = list(re.finditer(rb'(?im)^(?:DEBUG: [a-z0-9_.-]+:)?\s*(?:response status|status code|http status code):\s*([1-5][0-9]{2})\s*$', stderr))
    status_match = status_matches[-1] if status_matches else None
    # Only associate a request ID printed after the last observed response status.
    response_text = stderr[status_match.end():] if status_match else stderr
    service_matches = list(re.finditer(rb'(?im)(?:ErrorCode:\s*|[\"\']?x-ms-error-code[\"\']?\s*[:=]\s*[\"\']?)([A-Za-z][A-Za-z0-9]{0,63})', response_text))
    service_value = service_matches[-1].group(1).decode() if service_matches else None
    service = service_value if service_value in SERVICE_CODES else None
    request_matches = list(re.finditer(rb'(?im)^(?:DEBUG: [a-z0-9_.-]+:)?\s*(?:[\"\']?x-ms-request-id[\"\']?\s*[:=]\s*[\"\']?|requestid:\s*)([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})(?:[\"\']?\s*)$', response_text))
    request_match = request_matches[-1] if request_matches else None
    wrapper = recognized(('azure_cli_session_timeout', 'azure_cli_session_authentication_failed',
                          'azure_cli_session_assertion_expired', 'azure_cli_session_control_rejected',
                          'azure_cli_session_configuration_invalid'))
    stages = []
    aadsts_codes = ('AADSTS500011', 'AADSTS50020', 'AADSTS50076', 'AADSTS50079',
                    'AADSTS50126', 'AADSTS700016', 'AADSTS7000215', 'AADSTS7000218',
                    'AADSTS7000222', 'AADSTS700024', 'AADSTS700025', 'AADSTS700027',
                    'AADSTS700084', 'AADSTS70021', 'AADSTS700213', 'AADSTS90061')
    pattern = (rb'(?m)^azure_cli_session_stage pid=[0-9]+ stage=(lock|authentication|token_request|login|account_select|command|cleanup) '
               rb'event=(start|end|error|timeout|interrupted|exit|auth_code) elapsed_ms=[0-9]+'
               rb'(?: stage_ms=[0-9]+)?(?: status=(-?[0-9]+))?(?: aadsts=(AADSTS[0-9]+))?[^\S\r\n]*$')
    for stage, event, status, aadsts in re.findall(pattern, stderr):
        if len(stages) == 32: break
        row = {'stage': stage.decode(), 'event': event.decode(),
               'status': int(status) if status and len(status) <= 4 else None}
        if event == b'auth_code':
            value = aadsts.decode()
            if stage not in (b'login', b'account_select') or value not in aadsts_codes: continue
            row['aadsts'] = value
        elif aadsts:
            continue
        stages.append(row)
    if code == 0: category = 'success'
    elif wrapper: category = 'wrapper'
    elif status_match or service: category = 'storage_service'
    elif exception in ('FileNotFoundError', 'PermissionError', 'IsADirectoryError'): category = 'local_file'
    elif exception in ('NameResolutionError', 'gaierror'): category = 'dns'
    elif exception == 'SSLError': category = 'tls'
    elif exception in ('ConnectionError', 'ConnectTimeout', 'ReadTimeout', 'ServiceRequestError', 'ServiceResponseError'): category = 'network_or_sdk_transport'
    elif exception == 'ClientAuthenticationError': category = 'authentication'
    elif re.search(rb"(?i)unrecognized arguments|argument .* (?:required|invalid choice)|--only-show-errors can't be used together with --debug or --verbose", stderr): category = 'arguments'
    elif exception: category = 'client'
    else: category = 'unknown'
    return {'outcome': 'success' if code == 0 else 'failure', 'return_code': code,
            'category': category, 'service_code': service, 'exception_type': exception,
            'http_status': int(status_match.group(1)) if status_match else None,
            'request_id': public_request_id(request_match.group(1).decode()) if request_match else None,
            'wrapper_code': wrapper, 'wrapper_stages': stages}


def sdk_diagnostic(python, host, deadline, container="archive-smoke-source"):
    result = {'outcome': 'unavailable', 'token': None, 'read': None}
    if python is None:
        return result
    if deadline - time.monotonic() < 30:
        result['outcome'] = 'skipped_deadline'
        return result
    try:
        code, token, err = command(['az', 'account', 'get-access-token', '--resource',
                                    'https://storage.azure.com/', '--output', 'json', '--only-show-errors'],
                                   min(deadline, time.monotonic() + 15), cap=16384)
        result['token'] = command_evidence(code, err)
        if code:
            result['outcome'] = 'token_unavailable'
            return result
        token_data = json.loads(token, object_pairs_hook=pairs)
        token = token_data['accessToken']
        expires = token_data['expires_on']
        if isinstance(expires, str) and re.fullmatch(r'[0-9]{1,10}', expires): expires = int(expires)
        if not isinstance(token, str) or not re.fullmatch(r'[A-Za-z0-9._-]{1,8192}', token) or type(expires) is not int or not 0 < expires <= 4102444800:
            result['outcome'] = 'token_unavailable'
            return result
        payload = encoded({'host': host, 'container': container, 'token': token, 'expires_on': expires})
        code, out, _ = command([python, str(Path(__file__).with_name('smoke_sdk_diagnostic.py'))],
                               min(deadline, time.monotonic() + 15), cap=16384, input_data=payload)
        if code:
            result['outcome'] = 'helper_failed'
            return result
        data = json.loads(out, object_pairs_hook=pairs)
        if not isinstance(data, dict) or set(data) != {'outcome', 'exception_types', 'errno', 'http_status', 'service_code', 'request_id'}:
            raise ValueError()
        if data['outcome'] not in ('success', 'failure') or not isinstance(data['exception_types'], list) or len(data['exception_types']) > 16 or any(x not in EXCEPTION_TYPES for x in data['exception_types']):
            raise ValueError()
        if data['errno'] is not None and (type(data['errno']) is not int or not 0 <= data['errno'] <= 255): raise ValueError()
        if data['http_status'] is not None and (type(data['http_status']) is not int or not 100 <= data['http_status'] <= 599): raise ValueError()
        if data['service_code'] is not None and data['service_code'] not in SERVICE_CODES: raise ValueError()
        if data['request_id'] is not None and (not isinstance(data['request_id'], str) or not re.fullmatch(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', data['request_id'])): raise ValueError()
        data['request_id'] = public_request_id(data['request_id'])
        result.update(outcome='observed', read=data)
    except Gap as error:
        result['outcome'] = {'deadline': 'timeout', 'interrupted': 'interrupted'}.get(error.code, 'unavailable')
    except KeyboardInterrupt:
        result['outcome'] = 'interrupted'
    except Exception:
        result['outcome'] = 'unavailable'
    return result


def network_diagnostic(python, host, deadline):
    if python is None: return {'outcome': 'unavailable', 'observation': None}
    if deadline - time.monotonic() < 10: return {'outcome': 'skipped_deadline', 'observation': None}
    try:
        code, out, _ = command([python, str(Path(__file__).with_name('smoke_network_diagnostic.py'))],
                               min(deadline, time.monotonic() + 10), cap=16384,
                               input_data=encoded({'host': host}))
        if code: return {'outcome': 'helper_failed', 'observation': None}
        data = json.loads(out, object_pairs_hook=pairs)
        if not isinstance(data, dict) or set(data) != {'dns', 'tcp', 'tls', 'errno', 'tls_reason', 'tls_verify_code', 'tls_failure_kind'}: raise ValueError()
        if data['dns'] not in ('success', 'failure', 'empty', 'not_attempted'): raise ValueError()
        if data['tcp'] not in ('success', 'failure', 'not_attempted') or data['tls'] not in ('success', 'failure', 'not_attempted'): raise ValueError()
        if data['errno'] is not None and (type(data['errno']) is not int or not -255 <= data['errno'] <= 255): raise ValueError()
        reasons = ('CERTIFICATE_VERIFY_FAILED', 'WRONG_VERSION_NUMBER', 'TLSV1_ALERT_PROTOCOL_VERSION', 'SSLV3_ALERT_HANDSHAKE_FAILURE', 'UNEXPECTED_EOF_WHILE_READING', 'NO_CIPHERS_AVAILABLE', 'CERTIFICATE_REQUIRED', 'UNKNOWN_CA', 'TLSV1_ALERT_UNKNOWN_CA')
        if data['tls_reason'] is not None and data['tls_reason'] not in reasons: raise ValueError()
        if data['tls_failure_kind'] is not None and data['tls_failure_kind'] not in ('certificate_verification', 'ssl', 'timeout', 'configuration', 'os_error'): raise ValueError()
        if data['tls_verify_code'] is not None and (type(data['tls_verify_code']) is not int or not 0 <= data['tls_verify_code'] <= 255): raise ValueError()
        return {'outcome': 'observed', 'observation': data}
    except Gap as error:
        return {'outcome': {'deadline': 'timeout', 'interrupted': 'interrupted'}.get(error.code, 'unavailable'), 'observation': None}
    except KeyboardInterrupt:
        return {'outcome': 'interrupted', 'observation': None}
    except Exception:
        return {'outcome': 'unavailable', 'observation': None}


def run_probe(c, deadline, clock=time.monotonic, diagnostic_python=None, *, transport_versions):
    private, artifacts, host, source, ident, scenario, configuration = c
    prefix = 'v1/{}/{}/{}/{}/{}/'.format(ident['source_commit'], ident['github_run_id'], ident['github_run_attempt'], ident['run_id'], ident['phase'])
    src_name = prefix + 'synthetic-fixture.txt'; dest_name = prefix + 'synthetic-fixture.txt'
    gaps = []; checks = {}; manifest_ref = None; completion_ref = None
    diagnostics = []
    obj = {'kind': 'synthetic_fixture', 'source': {'container': 'archive-smoke-source', 'name': src_name, 'etag': None}, 'destination_name': dest_name, 'required': True, 'repeat': None, 'capture_id': None, 'bytes': None, 'sha256': None, 'destination_etag': None, 'state': 'failed', 'gap_code': None}
    totals = {'expected_objects': 1, 'verified_objects': 0, 'verified_bytes': 0}
    start = clock()
    public = artifacts / 'archive'; spool = private / 'archive'
    public.mkdir(mode=0o700, exist_ok=True); spool.mkdir(mode=0o700, exist_ok=True)
    if public.is_symlink() or spool.is_symlink():
        raise Invalid()
    def az(hostname, container, action, name, extra):
        check_deadline(deadline, clock)
        args = ['az', 'storage', 'blob', action, '--account-name', hostname.split('.')[0], '--container-name', container, '--name', name, '--auth-mode', 'login', '--debug'] + extra
        target = 'source' if hostname == source else 'archive'
        purpose = ('manifest' if name.endswith('archive-manifest.json') else
                   'completion' if name.endswith('archive-completion.json') else 'fixture')
        started = time.monotonic()
        record = {'target': target, 'object': purpose, 'operation': action, 'expected_negative': None,
                  'started_at': datetime.now(timezone.utc).isoformat(timespec='milliseconds').replace('+00:00', 'Z')}
        try:
            result = command(args, deadline)
            try:
                record.update(command_evidence(result[0], result[2]))
            except Exception:
                record.update(outcome='success' if result[0] == 0 else 'failure',
                              return_code=result[0], category='diagnostic_unavailable',
                              service_code=None, exception_type=None, http_status=None,
                              request_id=None, wrapper_code=None, wrapper_stages=[])
        except Gap as error:
            record.update(command_evidence(None, getattr(error, 'stderr', b'')))
            record.update(outcome='incomplete', category=error.boundary.replace(' ', '_'))
            raise
        except (OSError, subprocess.SubprocessError):
            record.update(command_evidence(None, b''))
            record.update(outcome='incomplete', category='process_launch')
            raise
        finally:
            record['elapsed_seconds'] = round(time.monotonic() - started, 3)
            diagnostics.append(record)
        check_deadline(deadline, clock)
        return result
    def show(hostname, container, name):
        code, out, _ = az(hostname, container, 'show', name, ['--output', 'json'])
        if code: raise Gap('transport', 'properties')
        try:
            data = json.loads(out, object_pairs_hook=pairs)
            length, etag = data['properties']['contentLength'], data['properties']['etag']
            if type(length) is not int or not 0 <= length <= MAX_OBJECT or not isinstance(etag, str) or not etag or len(etag) > 1024: raise ValueError()
            return length, etag
        except (Invalid, ValueError, KeyError, TypeError): raise Gap('transport', 'invalid properties') from None
    def upload(hostname, container, name, file):
        return az(hostname, container, 'upload', name, ['--file', str(file), '--if-none-match', '*', '--overwrite', 'false', '--max-connections', '1', '--output', 'none'])
    def download(hostname, container, name, etag, path):
        code, _, _ = az(hostname, container, 'download', name, ['--file', str(path), '--if-match', etag, '--overwrite', 'false', '--max-connections', '1', '--output', 'none'])
        if code: raise Gap('transport', 'conditional download')
    def verified_write(name, content):
        check_deadline(deadline, clock)
        cap = MIB if name.endswith('archive-manifest.json') else 65536
        if len(content) > cap: raise Gap('manifest_too_large' if cap == MIB else 'byte_limit', 'control size')
        path = spool / 'control'; back = spool / 'readback'
        safe_write(path, content); check_deadline(deadline, clock); back.unlink(missing_ok=True)
        code, _, _ = upload(host, 'run-evidence', name, path)
        if code: raise Gap('transport', 'control create')
        length, etag = show(host, 'run-evidence', name)
        download(host, 'run-evidence', name, etag, back)
        if length != len(content):
            raise Gap('destination_length_mismatch', 'control properties')
        if digest(back, length, deadline=deadline, clock=clock, side='destination') != hashlib.sha256(content).hexdigest():
            raise Gap('destination_hash_mismatch', 'control readback')
        path.unlink(missing_ok=True); back.unlink(missing_ok=True)
        return {'name': name, 'bytes': len(content), 'sha256': hashlib.sha256(content).hexdigest()}
    def add_gap(error):
        gaps.append({'code': error.code, 'boundary': error.boundary, 'object_name': None, 'expected_unit': None})
    try:
        check_deadline(deadline, clock)
        if shutil.disk_usage(spool).free < 2 * MAX_OBJECT + MIB + 65536: raise Gap('disk_space', 'spool reserve')
        fixture = spool / 'fixture'; downloaded = spool / 'download'; back = spool / 'readback'
        safe_write(fixture, FIXTURE); check_deadline(deadline, clock)
        code, _, _ = upload(source, 'archive-smoke-source', src_name, fixture)
        if code: raise Gap('transport', 'source create')
        length, etag = show(source, 'archive-smoke-source', src_name); obj['source']['etag'] = etag
        if length != len(FIXTURE): raise Gap('source_length_mismatch', 'source properties')
        download(source, 'archive-smoke-source', src_name, etag, downloaded)
        expected_hash = hashlib.sha256(FIXTURE).hexdigest()
        if digest(downloaded, length, deadline=deadline, clock=clock) != expected_hash: raise Gap('source_hash_mismatch', 'source readback')
        checks['source'] = True
        code, _, err = az(host, 'run-evidence', 'show', dest_name, ['--output', 'json'])
        if code == 0: raise Gap('destination_conflict', 'destination already exists')
        if not re.search(rb'(?i)\b(BlobNotFound|ResourceNotFound)\b', err): raise Gap('transport', 'destination pre-create properties')
        diagnostics[-1]['expected_negative'] = 'pre_create_not_found'
        code, _, _ = upload(host, 'run-evidence', dest_name, downloaded)
        if code: raise Gap('destination_conflict', 'destination create')
        length2, etag2 = show(host, 'run-evidence', dest_name)
        if length2 != length: raise Gap('destination_length_mismatch', 'destination properties')
        download(host, 'run-evidence', dest_name, etag2, back)
        if digest(back, length2, deadline=deadline, clock=clock, side='destination') != expected_hash: raise Gap('destination_hash_mismatch', 'destination readback')
        checks['destination'] = True
        # A distinct create-only attempt is the deliberate negative probe, not a retry.
        code, _, err = upload(host, 'run-evidence', dest_name, fixture)
        if code == 0 or not re.search(rb'(?i)\b(BlobAlreadyExists|ConditionNotMet)\b', err): raise Gap('destination_conflict', 'negative conflict classification')
        diagnostics[-1]['expected_negative'] = 'create_conflict'
        back.unlink(); length3, etag3 = show(host, 'run-evidence', dest_name)
        if length3 != length or etag3 != etag2: raise Gap('destination_conflict', 'negative unchanged properties')
        download(host, 'run-evidence', dest_name, etag3, back)
        if digest(back, length3, deadline=deadline, clock=clock, side='destination') != expected_hash: raise Gap('destination_hash_mismatch', 'negative unchanged content')
        checks['create_conflict'] = True
        obj.update(bytes=length, sha256=expected_hash, destination_etag=etag2, state='verified')
        totals.update(verified_objects=1, verified_bytes=length)
    except Gap as error:
        obj['gap_code'] = error.code; add_gap(error)
    except KeyboardInterrupt:
        obj['gap_code'] = 'interrupted'; add_gap(Gap('interrupted', 'probe interrupted'))
    except (OSError, subprocess.SubprocessError):
        obj['gap_code'] = 'transport'; add_gap(Gap('transport', 'local or command failure'))
    finally:
        for name in ('fixture', 'download', 'readback', 'control'):
            (spool / name).unlink(missing_ok=True)
    run = {**ident, 'scenario_id': scenario, 'configuration_id': configuration, 'images': {'loadgen': None, 'verifier': None, 'probe': None}, 'run_kind': 'smoke', 'purpose': 'archive-readiness', 'scientific_admissible': False}
    manifest = {'schema_version': 1, 'kind': 'run_evidence_archive', 'archive_complete': not gaps, 'run': run, 'destination': {'account_host': host, 'container': 'run-evidence', 'prefix': prefix, 'retention_days': 90}, 'policy': POLICY, 'expected_units': [], 'objects': [obj], 'captures': [], 'totals': totals, 'gaps': list(gaps), 'completion': None, 'transport_versions': dict(transport_versions)}
    content = encoded(manifest)
    try:
        manifest_ref = verified_write(prefix + 'archive-manifest.json', content)
        safe_write(public / 'archive-manifest.json', content)
        check_deadline(deadline, clock)
        if not gaps:
            marker = {'schema_version': 1, 'kind': 'run_evidence_archive_completion', 'identity': ident, 'manifest': manifest_ref, 'totals': totals, 'archive_complete': True}
            completion_ref = verified_write(prefix + 'archive-completion.json', encoded(marker))
            check_deadline(deadline, clock)
    except Gap as error:
        add_gap(error)
    except KeyboardInterrupt:
        add_gap(Gap('interrupted', 'control interrupted'))
    except (OSError, subprocess.SubprocessError):
        add_gap(Gap('transport', 'control failure'))
    finally:
        for name in ('control', 'readback'):
            (spool / name).unlink(missing_ok=True)
    try:
        check_deadline(deadline, clock)
    except Gap as error:
        if not gaps or gaps[-1]['code'] != 'deadline': add_gap(error)
    diagnostic_read = {}; diagnostic_network = {}
    targets = {'source': (source, 'archive-smoke-source'), 'archive': (host, 'run-evidence')}
    failed_targets = [d['target'] for d in diagnostics if d.get('outcome') != 'success'
                      and d.get('expected_negative') is None]
    first_target = failed_targets[0] if failed_targets else 'source'
    for target in (first_target, 'archive' if first_target == 'source' else 'source'):
        hostname, container = targets[target]
        diagnostic_read[target] = sdk_diagnostic(diagnostic_python, hostname, deadline, container) if gaps else {'outcome': 'not_needed', 'token': None, 'read': None}
        diagnostic_network[target] = network_diagnostic(diagnostic_python, hostname, deadline) if gaps else {'outcome': 'not_needed', 'observation': None}
    receipt = {'schema_version': 1, 'kind': 'run_evidence_archive_receipt', 'identity': ident, 'archive_complete': not gaps and completion_ref is not None, 'manifest': manifest_ref, 'completion': completion_ref if not gaps else None, 'gaps': gaps}
    report = {'schema_version': 1, 'kind': 'archive_smoke', 'synthetic': True, 'scientific_admissible': False, 'live_evidence': bool(checks), 'fresh_cli_connections': True, 'checks': checks, 'observed_bytes': totals['verified_bytes'], 'duration_seconds': round(clock() - start, 3), 'gaps': gaps, 'archive_complete': receipt['archive_complete'], 'cli_version': transport_versions['azure_cli'], 'storage_sdk_version': transport_versions['azure_storage_blob'], 'diagnostics': diagnostics, 'sdk_diagnostic': diagnostic_read, 'network_diagnostic': diagnostic_network}
    safe_write(public / 'archive-smoke.json', encoded(report))
    safe_write(public / 'archive-receipt.json', encoded(receipt))
    try:
        check_deadline(deadline, clock)
    except Gap as error:
        if receipt['archive_complete']:
            add_gap(error)
            receipt.update(archive_complete=False, completion=None, gaps=gaps)
            report.update(archive_complete=False, gaps=gaps)
            for filename, value in (('archive-receipt.json', receipt), ('archive-smoke.json', report)):
                temp = public / (filename + '.partial')
                safe_write(temp, encoded(value)); os.replace(temp, public / filename)
    return 0 if receipt['archive_complete'] else 1


def failure_report(c, error):
    public = c[1] / 'archive'
    public.mkdir(mode=0o700, exist_ok=True)
    if public.is_symlink(): raise Invalid()
    gap = {'code': error.code, 'boundary': error.boundary, 'object_name': None, 'expected_unit': None}
    receipt = {'schema_version': 1, 'kind': 'run_evidence_archive_receipt', 'identity': c[4], 'archive_complete': False, 'manifest': None, 'completion': None, 'gaps': [gap]}
    report = {'schema_version': 1, 'kind': 'archive_smoke', 'synthetic': True, 'scientific_admissible': False, 'live_evidence': False, 'fresh_cli_connections': False, 'checks': {}, 'observed_bytes': 0, 'duration_seconds': 0, 'gaps': [gap], 'archive_complete': False, 'cli_version': None, 'storage_sdk_version': None, 'diagnostics': [], 'sdk_diagnostic': {}, 'network_diagnostic': {}}
    for name, data in (('archive-smoke.json', report), ('archive-receipt.json', receipt)):
        with contextlib.suppress(OSError):
            safe_write(public / name, encoded(data))


def main(argv):
    if len(argv) != 1 or argv[0] not in ('preflight', 'run'):
        return 2
    deadline = time.monotonic() + DEADLINE
    previous = signal.signal(signal.SIGTERM, lambda *_: (_ for _ in ()).throw(KeyboardInterrupt()))
    c = None
    try:
        c = config(False)
        if argv[0] == 'run':
            c = config(True)
        found = preflight(deadline)
        return 0 if argv[0] == 'preflight' else run_probe(c, deadline, diagnostic_python=found['python'], transport_versions={'azure_cli': found['azure_cli'], 'azure_storage_blob': found['azure_storage_blob']})
    except Invalid:
        if c is not None:
            with contextlib.suppress(OSError, Invalid): failure_report(c, Gap('config_invalid', 'unsupported configuration'))
        print('archive smoke: invalid or unsupported configuration', file=sys.stderr)
        return 2
    except (OSError, Gap, KeyboardInterrupt) as error:
        if c is not None:
            gap = error if isinstance(error, Gap) else Gap('interrupted' if isinstance(error, KeyboardInterrupt) else 'transport', 'probe incomplete')
            with contextlib.suppress(OSError, Invalid): failure_report(c, gap)
        print('archive smoke: incomplete', file=sys.stderr)
        return 1
    finally:
        signal.signal(signal.SIGTERM, previous)

if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))

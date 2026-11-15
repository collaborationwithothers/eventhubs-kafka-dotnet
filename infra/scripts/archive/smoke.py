#!/usr/bin/env python3
"""Bounded synthetic archive readiness probe; no scientific evidence is produced."""
import contextlib
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
DEADLINE = 600
FIXTURE = b'Parchwell archive readiness synthetic fixture v1\n'
POLICY = {'max_bytes': 17179869184, 'max_object_bytes': MAX_OBJECT,
          'max_objects': 1024, 'deadline_seconds': 600, 'transfer_passes': 1,
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


def command(args, deadline, cap=MIB):
    end = min(time.monotonic() + 60, deadline)
    if end <= time.monotonic():
        raise Gap('deadline', 'global deadline')
    pending = []
    previous = {sig: signal.signal(sig, lambda number, _: pending.append(number))
                for sig in (signal.SIGINT, signal.SIGTERM)}
    try:
        proc = subprocess.Popen(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                start_new_session=True, preexec_fn=limit_child)
    finally:
        for sig, handler in previous.items(): signal.signal(sig, handler)
    buffers = {proc.stdout: bytearray(), proc.stderr: bytearray()}
    try:
        if pending: raise KeyboardInterrupt()
        with selectors.DefaultSelector() as selector:
            for stream in buffers:
                selector.register(stream, selectors.EVENT_READ)
            while selector.get_map():
                remaining = end - time.monotonic()
                if remaining <= 0:
                    raise Gap('deadline', 'command timeout')
                for key, _ in selector.select(remaining):
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
        raise Gap('interrupted', 'command interrupted') from None
    except Gap:
        stop_command(proc)
        raise
    except subprocess.TimeoutExpired:
        stop_command(proc)
        raise Gap('deadline', 'command timeout') from None
    except BaseException:
        stop_command(proc)
        raise
    finally:
        proc.stdout.close(); proc.stderr.close()


def preflight(deadline):
    try:
        code, output, _ = command(['az', 'version', '--output', 'json'], deadline)
        version = json.loads(output, object_pairs_hook=pairs)
        if code or not isinstance(version, dict) or version.get('azure-cli') != '2.92.0':
            raise Invalid()
        code, output, _ = command(['az', '--version'], deadline)
        matches = re.findall(rb"(?m)^Python location ['\"]([^'\"\r\n]+)['\"]\s*$", output)
        if code or len(matches) != 1:
            raise Invalid()
        python = os.fsdecode(matches[0])
        if not os.path.isabs(python) or not os.path.isfile(python) or not os.access(python, os.X_OK):
            raise Invalid()
        code, output, _ = command([python, '-c', 'import importlib.metadata; print(importlib.metadata.version("azure-storage-blob"))'], deadline)
        if code or output.strip() != b'12.29.0b1':
            raise Invalid()
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


def run_probe(c, deadline, clock=time.monotonic):
    private, artifacts, host, source, ident, scenario, configuration = c
    prefix = 'v1/{}/{}/{}/{}/{}/'.format(ident['source_commit'], ident['github_run_id'], ident['github_run_attempt'], ident['run_id'], ident['phase'])
    src_name = prefix + 'synthetic-fixture.txt'; dest_name = prefix + 'synthetic-fixture.txt'
    gaps = []; checks = {}; manifest_ref = None; completion_ref = None
    obj = {'kind': 'synthetic_fixture', 'source': {'container': 'archive-smoke-source', 'name': src_name, 'etag': None}, 'destination_name': dest_name, 'required': True, 'repeat': None, 'capture_id': None, 'bytes': None, 'sha256': None, 'destination_etag': None, 'state': 'failed', 'gap_code': None}
    totals = {'expected_objects': 1, 'verified_objects': 0, 'verified_bytes': 0}
    start = clock()
    public = artifacts / 'archive'; spool = private / 'archive'
    public.mkdir(mode=0o700, exist_ok=True); spool.mkdir(mode=0o700, exist_ok=True)
    if public.is_symlink() or spool.is_symlink():
        raise Invalid()
    def az(hostname, container, action, name, extra):
        check_deadline(deadline, clock)
        args = ['az', 'storage', 'blob', action, '--account-name', hostname.split('.')[0], '--container-name', container, '--name', name, '--auth-mode', 'login', '--only-show-errors'] + extra
        result = command(args, deadline)
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
    manifest = {'schema_version': 1, 'kind': 'run_evidence_archive', 'archive_complete': not gaps, 'run': run, 'destination': {'account_host': host, 'container': 'run-evidence', 'prefix': prefix, 'retention_days': 90}, 'policy': POLICY, 'expected_units': [], 'objects': [obj], 'captures': [], 'totals': totals, 'gaps': list(gaps), 'completion': None}
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
    receipt = {'schema_version': 1, 'kind': 'run_evidence_archive_receipt', 'identity': ident, 'archive_complete': not gaps and completion_ref is not None, 'manifest': manifest_ref, 'completion': completion_ref if not gaps else None, 'gaps': gaps}
    report = {'schema_version': 1, 'kind': 'archive_smoke', 'synthetic': True, 'scientific_admissible': False, 'live_evidence': bool(checks), 'fresh_cli_connections': True, 'checks': checks, 'observed_bytes': totals['verified_bytes'], 'duration_seconds': round(clock() - start, 3), 'gaps': gaps, 'archive_complete': receipt['archive_complete'], 'cli_version': '2.92.0', 'storage_sdk_version': '12.29.0b1'}
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
    report = {'schema_version': 1, 'kind': 'archive_smoke', 'synthetic': True, 'scientific_admissible': False, 'live_evidence': False, 'fresh_cli_connections': False, 'checks': {}, 'observed_bytes': 0, 'duration_seconds': 0, 'gaps': [gap], 'archive_complete': False, 'cli_version': None, 'storage_sdk_version': None}
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
        preflight(deadline)
        return 0 if argv[0] == 'preflight' else run_probe(c, deadline)
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

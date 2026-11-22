#!/usr/bin/env python3
"""Download and verify the pinned companion run's C5 evidence artifacts."""
import argparse
from datetime import datetime, timezone
from email.utils import parsedate_to_datetime
import hashlib
from http.client import HTTPException
import json
import math
import os
from pathlib import Path, PurePosixPath
import re
import stat
import sys
import tempfile
import time
from urllib.error import HTTPError
from urllib.parse import urlparse
from urllib.request import HTTPRedirectHandler, Request, build_opener
import zipfile

import c5_campaign
import c5_runtime

API = 'https://api.github.com'
SPACING = 0.2
DEADLINE = 240
TIMEOUT = 15
ATTEMPTS = 4


class DownloadError(Exception):
    pass


class HTTPSRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        if urlparse(newurl).scheme.lower() != 'https':
            raise DownloadError('refused non-HTTPS redirect')
        return super().redirect_request(req, fp, code, msg, headers, newurl)


class Transport:
    def __init__(self, token):
        if not token:
            raise DownloadError('GH_TOKEN is required')
        self.token = token
        self.opener = build_opener(HTTPSRedirect())
        self.deadline = time.monotonic() + DEADLINE
        self.last_request = None

    def pause(self, seconds):
        remaining = self.deadline - time.monotonic()
        if seconds >= remaining:
            raise DownloadError('transport deadline exceeded')
        if seconds > 0:
            time.sleep(seconds)

    def request(self, path, consume):
        for attempt in range(ATTEMPTS):
            if self.last_request is not None:
                self.pause(max(0, self.last_request + SPACING - time.monotonic()))
            remaining = self.deadline - time.monotonic()
            if remaining <= 0:
                raise DownloadError('transport deadline exceeded')
            request = Request(API + path, headers={'Accept': 'application/vnd.github+json',
                              'X-GitHub-Api-Version': '2022-11-28', 'User-Agent': 'c5-companion-artifacts'})
            # urllib does not forward unredirected headers to the signed blob URL.
            request.add_unredirected_header('Authorization', 'Bearer ' + self.token)
            self.last_request = time.monotonic()
            try:
                with self.opener.open(request, timeout=min(TIMEOUT, remaining)) as response:
                    return consume(response, self.deadline)
            except HTTPError as error:
                status = error.code
                headers = error.headers
                try:
                    with error:
                        body = read_response(error, self.deadline, limit=8192).decode('utf-8', 'replace').lower()
                except (HTTPException, OSError):
                    body = ''
                primary = status in (403, 429) and headers.get('X-RateLimit-Remaining') == '0'
                secondary = status == 429 or (
                    status == 403 and (headers.get('Retry-After') is not None or 'secondary rate limit' in body))
                transient = status == 408 or 500 <= status <= 599 or secondary or primary
                if not transient:
                    raise DownloadError(f'GitHub request failed (HTTP {status})') from None
                delay = retry_delay(headers, secondary, primary, attempt)
                failure = f'HTTP {status} rate limit' if secondary or primary else f'HTTP {status}'
            except (HTTPException, OSError) as error:
                delay = 2 ** attempt
                failure = type(error).__name__
            except ValueError:
                raise DownloadError('invalid GitHub response or redirect') from None
            if attempt == ATTEMPTS - 1:
                raise DownloadError(f'GitHub request exhausted retries ({failure})') from None
            print(f'GitHub request failed ({failure}). Retry delay {delay:.2f}s.', file=sys.stderr)
            self.pause(delay)
        raise DownloadError('GitHub request exhausted retries')


def retry_delay(headers, secondary, primary, attempt):
    delay = 2 ** attempt
    value = headers.get('Retry-After')
    retry_after = None
    if value:
        try:
            retry_after = float(value)
            if retry_after < 0:
                retry_after = None
        except ValueError:
            try:
                retry_after = parsedate_to_datetime(value).timestamp() - time.time()
            except (ValueError, TypeError, OverflowError):
                pass
        if retry_after is not None and not math.isfinite(retry_after):
            retry_after = None
    reset = None
    if primary:
        try:
            reset = float(headers['X-RateLimit-Reset']) - time.time()
            if not math.isfinite(reset):
                reset = None
        except (KeyError, ValueError):
            pass
    if retry_after is not None or reset is not None:
        delay = max(delay, retry_after or 0, reset or 0)
    elif secondary or primary:
        delay = max(delay, 60 * (2 ** attempt))
    return delay


def read_response(response, deadline, limit=None):
    chunks = []
    while limit is None or limit > 0:
        if time.monotonic() >= deadline:
            raise DownloadError('transport deadline exceeded')
        chunk = response.read1(65536 if limit is None else min(65536, limit))
        if not chunk:
            break
        chunks.append(chunk)
        if limit is not None:
            limit -= len(chunk)
    return b''.join(chunks)


def catalog(transport, repository, identity, commit, shards):
    run_id, attempt = identity['id'], identity['attempt']
    run = json.loads(transport.request(f'/repos/{repository}/actions/runs/{run_id}/attempts/{attempt}', read_response))
    created = c5_campaign.validate_run(run, 'companion', repository, commit, run_id, attempt)
    c5_campaign.require(created == c5_campaign.timestamp(identity['created_at']), 'companion creation time mismatch')
    prefix = f'c5-mutation-{run_id}-{attempt}-'
    expected = {prefix + shard for shard in shards}
    rows, total = [], None
    for page in range(1, 11):
        data = json.loads(transport.request(f'/repos/{repository}/actions/runs/{run_id}/artifacts?per_page=100&page={page}', read_response))
        c5_campaign.require(isinstance(data, dict) and type(data.get('total_count')) is int
                            and 0 <= data['total_count'] <= 1000 and isinstance(data.get('artifacts'), list), 'invalid artifact catalog')
        c5_campaign.require(total is None or total == data['total_count'], 'artifact count changed')
        total = data['total_count']
        batch = data['artifacts']
        c5_campaign.require(len(batch) <= 100, 'invalid artifact page')
        rows.extend(batch)
        c5_campaign.require(len(rows) <= total, 'extra artifacts')
        if len(rows) == total:
            break
        c5_campaign.require(len(batch) == 100, 'missing artifact page')
    c5_campaign.require(len(rows) == total, 'artifact catalog exceeds page limit')
    selected, ids = {}, set()
    for artifact in rows:
        c5_campaign.require(isinstance(artifact, dict) and isinstance(artifact.get('name'), str), 'invalid artifact')
        name = artifact['name']
        if not name.startswith(prefix):
            continue
        info = artifact.get('workflow_run')
        digest = artifact.get('digest')
        c5_campaign.require(name in expected and name not in selected and type(artifact.get('id')) is int
                            and artifact['id'] > 0 and artifact['id'] not in ids and artifact.get('expired') is False
                            and type(artifact.get('size_in_bytes')) is int and artifact['size_in_bytes'] > 0
                            and isinstance(digest, str) and re.fullmatch(r'sha256:[0-9a-f]{64}', digest)
                            and isinstance(info, dict) and info.get('id') == run_id and info.get('head_sha') == commit,
                            'invalid, duplicate, expired or foreign companion artifact')
        selected[name] = artifact
        ids.add(artifact['id'])
    return selected, expected - selected.keys()


def save_archive(response, deadline, path, expected_size):
    digest = hashlib.sha256()
    size = 0
    with path.open('wb') as stream:
        while True:
            if time.monotonic() >= deadline:
                raise DownloadError('transport deadline exceeded')
            chunk = response.read1(65536)
            if not chunk:
                break
            size += len(chunk)
            if size > expected_size:
                raise DownloadError('artifact size mismatch')
            stream.write(chunk)
            digest.update(chunk)
    return size, digest.hexdigest()


def extract(archive, destination):
    names = set()
    with zipfile.ZipFile(archive) as zipped:
        for item in zipped.infolist():
            name = item.filename
            path = PurePosixPath(name)
            if (not name or '\\' in name or name.startswith('/') or any(p in ('', '.', '..') for p in name.rstrip('/').split('/'))
                    or path.is_absolute() or (path.parts and ':' in path.parts[0]) or name.rstrip('/') in names
                    or stat.S_ISLNK(item.external_attr >> 16)):
                raise DownloadError('unsafe or duplicate archive entry')
            names.add(name.rstrip('/'))
        if 'receipt.json' not in names or not any(i.filename == 'receipt.json' and not i.is_dir() for i in zipped.infolist()):
            raise DownloadError('archive missing receipt.json')
        zipped.extractall(destination)


def download(transport, repository, name, artifact, output):
    output.mkdir(parents=True, exist_ok=True)
    target = output / name
    if target.exists() or target.is_symlink():
        raise DownloadError('artifact output already exists')
    with tempfile.TemporaryDirectory(prefix='.c5-download-', dir=output) as temporary:
        tmp = Path(temporary)
        archive, extracted = tmp / 'artifact.zip', tmp / 'extracted'
        size, digest = transport.request(
            f'/repos/{repository}/actions/artifacts/{artifact["id"]}/zip',
            lambda response, deadline: save_archive(response, deadline, archive, artifact['size_in_bytes']))
        if size != artifact['size_in_bytes'] or 'sha256:' + digest != artifact['digest']:
            raise DownloadError('artifact size or digest mismatch')
        extracted.mkdir()
        try:
            extract(archive, extracted)
        except (zipfile.BadZipFile, RuntimeError, OSError, ValueError):
            raise DownloadError('invalid artifact archive') from None
        os.rename(extracted, target)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for option in ('repository', 'commit', 'manifest', 'output-dir'):
        parser.add_argument('--' + option, required=True)
    args = parser.parse_args()
    try:
        manifest = c5_runtime.strict_json(Path(args.manifest).read_bytes())
        layout = c5_campaign.matrices()
        c5_campaign.validate_manifest(manifest, args.commit, layout['coordinator'], layout['companion'])
        c5_campaign.require(manifest['repository'] == args.repository, 'manifest repository mismatch')
        transport = Transport(os.environ.get('GH_TOKEN'))
        artifacts, missing = catalog(transport, args.repository, manifest['runs']['companion'], args.commit,
                                     layout['companion'])
        for name, artifact in sorted(artifacts.items()):
            try:
                download(transport, args.repository, name, artifact, Path(args.output_dir))
            except (DownloadError, OSError) as error:
                message = str(error) if isinstance(error, DownloadError) else 'artifact I/O error'
                raise DownloadError(f'{name}: {message}') from None
        if missing:
            raise DownloadError(f'missing companion artifacts: {", ".join(sorted(missing))}')
        print(f'Verified {len(artifacts)} companion evidence archives.')
        return 0
    except (DownloadError, OSError, ValueError, KeyError, TypeError) as error:
        # Avoid exposing transport exception details, including signed URLs and credentials.
        print(f'Companion artifact download failed: {error if isinstance(error, (DownloadError, ValueError)) else "invalid input or I/O error"}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())

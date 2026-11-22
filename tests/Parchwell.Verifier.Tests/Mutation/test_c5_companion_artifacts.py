import hashlib
import io
import json
from pathlib import Path
import stat
import tempfile
import unittest
from unittest.mock import patch
from urllib.error import HTTPError, URLError
import zipfile

import c5_companion_artifacts as companion


class Response(io.BytesIO):
    def __enter__(self):
        return self

    def __exit__(self, *args):
        self.close()


def archive(entries):
    data = io.BytesIO()
    with zipfile.ZipFile(data, 'w') as z:
        for name, content in entries:
            z.writestr(name, content)
    return data.getvalue()


def artifact(name, number, data=b'zip'):
    return {'name': name, 'id': number, 'expired': False, 'size_in_bytes': len(data),
            'digest': 'sha256:' + hashlib.sha256(data).hexdigest(),
            'workflow_run': {'id': 11, 'head_sha': 'a' * 40}}


class CatalogTests(unittest.TestCase):
    def test_pagination_and_exact_attempt(self):
        identity = {'id': 11, 'attempt': 2, 'created_at': '2025-01-01T00:00:00Z'}
        run = {'id': 11, 'run_attempt': 2, 'path': companion.c5_campaign.PATHS['companion'],
               'event': 'workflow_dispatch', 'head_branch': 'main', 'head_sha': 'a' * 40,
               'repository': {'full_name': 'owner/repo'}, 'head_repository': {'full_name': 'owner/repo'},
               'created_at': identity['created_at']}
        expected = artifact('c5-mutation-11-2-shard', 1)
        other = [{'name': f'c5-logs-{i}'} for i in range(99)]
        pages = iter([run, {'total_count': 101, 'artifacts': [expected] + other},
                      {'total_count': 101, 'artifacts': [{'name': 'c5-mutation-11-1-old'}]}])
        class Fake:
            def request(self, path, consume):
                return json.dumps(next(pages)).encode()
        self.assertEqual(companion.catalog(Fake(), 'owner/repo', identity, 'a' * 40, ['shard']),
                         {expected['name']: expected})

    def test_missing_foreign_duplicate_or_expired_artifacts_rejected(self):
        identity = {'id': 11, 'attempt': 2, 'created_at': '2025-01-01T00:00:00Z'}
        class Fake:
            def request(self, path, consume):
                return json.dumps({'total_count': len(rows), 'artifacts': rows}).encode()
        expected = artifact('c5-mutation-11-2-shard', 1)
        with patch.object(companion.c5_campaign, 'validate_run',
                          return_value=companion.c5_campaign.timestamp(identity['created_at'])):
            for rows in ([], [artifact('c5-mutation-11-1-shard', 1)],
                         [artifact('c5-mutation-11-2-foreign', 1)], [expected, expected],
                         [{**expected, 'expired': True}], [{**expected, 'id': 0}],
                         [{**expected, 'workflow_run': {'id': 12, 'head_sha': 'a' * 40}}]):
                with self.subTest(rows=rows), self.assertRaises(ValueError):
                    companion.catalog(Fake(), 'owner/repo', identity, 'a' * 40, ['shard'])

    def test_catalogue_limit_accepts_999_and_1000_rows_and_rejects_1001(self):
        identity = {'id': 11, 'attempt': 2, 'created_at': '2025-01-01T00:00:00Z'}
        expected = artifact('c5-mutation-11-2-shard', 1)
        with patch.object(companion.c5_campaign, 'validate_run',
                          return_value=companion.c5_campaign.timestamp(identity['created_at'])):
            for count in (999, 1000, 1001):
                rows = [expected, *({'name': f'c5-logs-{i}'} for i in range(count - 1))]
                responses = iter([{}, *({'total_count': count, 'artifacts': rows[i:i + 100]}
                                        for i in range(0, count, 100))])

                class Fake:
                    def request(self, path, consume):
                        return json.dumps(next(responses)).encode()

                with self.subTest(count=count):
                    if count <= 1000:
                        self.assertEqual(companion.catalog(Fake(), 'owner/repo', identity, 'a' * 40, ['shard']),
                                         {expected['name']: expected})
                    else:
                        with self.assertRaisesRegex(ValueError, 'invalid artifact catalog'):
                            companion.catalog(Fake(), 'owner/repo', identity, 'a' * 40, ['shard'])


class TransportTests(unittest.TestCase):
    def test_retry_delays_and_exhaustion(self):
        clock = [0.0]
        calls = []
        def monotonic():
            return clock[0]
        def sleep(seconds):
            clock[0] += seconds
        def open_request(request, timeout):
            calls.append(clock[0])
            raise HTTPError(request.full_url, 403, 'limited', {'Retry-After': '1'}, io.BytesIO(b'secondary rate limit'))
        with patch.object(companion.time, 'monotonic', side_effect=monotonic), patch.object(companion.time, 'sleep', side_effect=sleep):
            transport = companion.Transport('secret')
            transport.opener.open = open_request
            with self.assertRaises(companion.DownloadError):
                transport.request('/x', companion.read_response)
        self.assertEqual(calls, [0, 1, 3, 7])
        self.assertEqual(companion.retry_delay({}, True, False, 1), 120)
        with patch.object(companion.time, 'time', return_value=100):
            self.assertEqual(companion.retry_delay({'X-RateLimit-Reset': '150'}, False, True, 0), 50)
            self.assertEqual(companion.retry_delay({'X-RateLimit-Reset': '150', 'Retry-After': '1'}, True, True, 0), 50)
            self.assertEqual(companion.retry_delay({'Retry-After': 'Thu, 01 Jan 1970 00:02:00 GMT'}, False, False, 0), 20)

    def test_retry_beyond_deadline_never_makes_another_request(self):
        with patch.object(companion.time, 'monotonic', return_value=0):
            transport = companion.Transport('synthetic')
            transport.deadline = 1
            error = HTTPError('https://api.github.com/x', 429, 'limited', {'Retry-After': '1'}, io.BytesIO(b'limited'))
            with patch.object(transport.opener, 'open', side_effect=error) as request, \
                    patch.object(companion.time, 'sleep') as sleep, \
                    self.assertRaisesRegex(companion.DownloadError, 'deadline exceeded'):
                transport.request('/x', companion.read_response)
            request.assert_called_once()
            sleep.assert_not_called()

    def test_body_stops_starting_reads_at_the_deadline(self):
        for now, readable in ((239.99, True), (240, False), (240.01, False)):
            with self.subTest(now=now), patch.object(companion.time, 'monotonic', return_value=now):
                response = Response(b'body')
                if readable:
                    self.assertEqual(companion.read_response(response, 240), b'body')
                else:
                    with self.assertRaisesRegex(companion.DownloadError, 'deadline exceeded'):
                        companion.read_response(response, 240)
                    self.assertEqual(response.tell(), 0)

    def test_permanent_error_does_not_retry_and_redirection_omits_token(self):
        transport = companion.Transport('secret')
        calls = []
        def denied(request, timeout):
            calls.append(request)
            raise HTTPError(request.full_url, 403, 'denied', {}, io.BytesIO(b'forbidden'))
        transport.opener.open = denied
        with self.assertRaises(companion.DownloadError):
            transport.request('/x', companion.read_response)
        self.assertEqual(len(calls), 1)
        redirected = companion.HTTPSRedirect().redirect_request(calls[0], None, 302, '', {}, 'https://blob.example/file')
        self.assertIsNone(redirected.get_header('Authorization'))
        with self.assertRaises(companion.DownloadError):
            companion.HTTPSRedirect().redirect_request(calls[0], None, 302, '', {}, 'http://blob.example/file')

    def test_network_retry_and_spacing(self):
        clock = [0.0]
        calls = []
        def open_request(request, timeout):
            calls.append(clock[0])
            if len(calls) == 1:
                raise URLError('private url')
            return Response(b'ok')
        with patch.object(companion.time, 'monotonic', side_effect=lambda: clock[0]), patch.object(companion.time, 'sleep', side_effect=lambda n: clock.__setitem__(0, clock[0] + n)):
            transport = companion.Transport('secret')
            transport.opener.open = open_request
            self.assertEqual(transport.request('/x', companion.read_response), b'ok')
            self.assertEqual(transport.request('/y', companion.read_response), b'ok')
        self.assertGreaterEqual(calls[1] - calls[0], 1)
        self.assertAlmostEqual(calls[2] - calls[1], .2)


class ArchiveTests(unittest.TestCase):
    def test_verified_archive_is_placed_under_exact_name(self):
        data = archive([('receipt.json', '{}'), ('nested/result.json', '{}')])
        name = 'c5-mutation-11-2-shard'
        class Fake:
            def request(self, path, consume):
                return consume(Response(data), float('inf'))
        with tempfile.TemporaryDirectory() as root:
            companion.download(Fake(), 'owner/repo', name, artifact(name, 1, data), Path(root))
            self.assertEqual((Path(root) / name / 'receipt.json').read_text(), '{}')
            self.assertTrue((Path(root) / name / 'nested/result.json').exists())

    def test_bad_missing_and_unsafe_archives_leave_no_output(self):
        symlink = zipfile.ZipInfo('link')
        symlink.create_system = 3
        symlink.external_attr = (stat.S_IFLNK | 0o777) << 16
        samples = [b'broken', archive([('result', '')]), archive([('../receipt.json', '')]),
                   archive([('receipt.json', ''), ('receipt.json', '')]),
                   archive([('receipt.json', '{}'), (symlink, '/tmp/file')])]
        for data in samples:
            with self.subTest(data=data), tempfile.TemporaryDirectory() as root:
                class Fake:
                    def request(self, path, consume):
                        return consume(Response(data), float('inf'))
                with self.assertRaises(companion.DownloadError):
                    companion.download(Fake(), 'owner/repo', 'name', artifact('name', 1, data), Path(root))
                self.assertFalse((Path(root) / 'name').exists())

    def test_digest_mismatch_is_rejected(self):
        data = archive([('receipt.json', '{}')])
        class Fake:
            def request(self, path, consume):
                return consume(Response(data), float('inf'))
        with tempfile.TemporaryDirectory() as root, self.assertRaisesRegex(companion.DownloadError, 'digest mismatch'):
            companion.download(Fake(), 'owner/repo', 'name', artifact('name', 1, b'x' * len(data)), Path(root))
        for wrong_size in (len(data) - 1, len(data) + 1):
            with self.subTest(wrong_size=wrong_size), tempfile.TemporaryDirectory() as root, \
                    self.assertRaisesRegex(companion.DownloadError, 'size'):
                companion.download(Fake(), 'owner/repo', 'name',
                                   {**artifact('name', 1, data), 'size_in_bytes': wrong_size}, Path(root))


class DownloadCommandTests(unittest.TestCase):
    def test_stops_after_retry_exhaustion(self):
        manifest = {'repository': 'owner/repo', 'runs': {'companion': {}}}
        rows = {name: {} for name in ('first', 'second', 'third')}
        command = ['downloader', '--repository', 'owner/repo', '--commit', 'a' * 40,
                   '--manifest', 'unused.json', '--output-dir', 'unused']
        effects = [None, companion.DownloadError('GitHub request exhausted retries')]
        with patch.object(companion.sys, 'argv', command), patch.dict(companion.os.environ, {'GH_TOKEN': 'synthetic'}), \
                patch.object(companion.Path, 'read_bytes', return_value=json.dumps(manifest).encode()), \
                patch.object(companion.c5_campaign, 'matrices', return_value={'companion': {}, 'coordinator': {}}), \
                patch.object(companion.c5_campaign, 'validate_manifest'), \
                patch.object(companion, 'catalog', return_value=rows), \
                patch.object(companion, 'download', side_effect=effects) as download, \
                patch.object(companion.sys, 'stderr', io.StringIO()):
            self.assertEqual(companion.main(), 1)
        self.assertEqual([call.args[2] for call in download.call_args_list], ['first', 'second'])


if __name__ == '__main__':
    unittest.main()

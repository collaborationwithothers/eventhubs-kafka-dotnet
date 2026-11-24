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
                         ({expected['name']: expected}, set()))

    def test_foreign_duplicate_or_expired_artifacts_rejected(self):
        identity = {'id': 11, 'attempt': 2, 'created_at': '2025-01-01T00:00:00Z'}
        class Fake:
            def request(self, path, consume):
                return json.dumps({'total_count': len(rows), 'artifacts': rows}).encode()
        expected = artifact('c5-mutation-11-2-shard', 1)
        with patch.object(companion.c5_campaign, 'validate_run',
                          return_value=companion.c5_campaign.timestamp(identity['created_at'])):
            for rows in ([artifact('c5-mutation-11-2-foreign', 1)], [expected, expected],
                         [{**expected, 'expired': True}], [{**expected, 'id': 0}],
                         [{**expected, 'workflow_run': {'id': 12, 'head_sha': 'a' * 40}}]):
                with self.subTest(rows=rows), self.assertRaises(ValueError):
                    companion.catalog(Fake(), 'owner/repo', identity, 'a' * 40, ['shard'])

    def test_empty_and_older_attempt_catalogues_report_missing_current_artifacts(self):
        identity = {'id': 11, 'attempt': 2, 'created_at': '2025-01-01T00:00:00Z'}

        class Fake:
            def request(self, path, consume):
                return json.dumps({'total_count': len(rows), 'artifacts': rows}).encode()

        with patch.object(companion.c5_campaign, 'validate_run',
                          return_value=companion.c5_campaign.timestamp(identity['created_at'])):
            for rows in ([], [artifact('c5-mutation-11-1-shard', 1)]):
                with self.subTest(rows=rows):
                    self.assertEqual(companion.catalog(Fake(), 'owner/repo', identity, 'a' * 40, ['shard']),
                                     ({}, {'c5-mutation-11-2-shard'}))

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
                                         ({expected['name']: expected}, set()))
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

    def test_split_secondary_limit_message_respects_error_body_cap(self):
        phrase = b'secondary rate limit'
        for size, retryable in ((8191, True), (8192, True), (8193, False)):
            clock = [0]
            body = Response(b' ' * (size - len(phrase)) + phrase)
            read1 = body.read1
            error = HTTPError('https://api.github.com/x', 403, 'limited', {}, body)
            with self.subTest(size=size), patch.object(body, 'read1', side_effect=lambda n: read1(min(n, 4090))) as read, \
                    patch.object(companion.time, 'monotonic', side_effect=lambda: clock[0]), \
                    patch.object(companion.time, 'sleep', side_effect=lambda delay: clock.__setitem__(0, clock[0] + delay)) as sleep:
                transport = companion.Transport('synthetic')
                with patch.object(transport.opener, 'open', side_effect=[error, Response(b'ok')]) as request:
                    if retryable:
                        self.assertEqual(transport.request('/x', companion.read_response), b'ok')
                        self.assertEqual(request.call_count, 2)
                        sleep.assert_called_once_with(60)
                    else:
                        with self.assertRaisesRegex(companion.DownloadError, 'HTTP 403'):
                            transport.request('/x', companion.read_response)
                        request.assert_called_once()
                        sleep.assert_not_called()
            self.assertEqual([call.args[0] for call in read.call_args_list],
                             [8192, 4102, 12, 1] if size == 8191 else [8192, 4102, 12])

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

    def test_transport_deadline_is_600_seconds_from_construction(self):
        clock = [0]
        with patch.object(companion.time, 'monotonic', side_effect=lambda: clock[0]):
            transport = companion.Transport('synthetic')
            self.assertEqual(600, transport.deadline)
            timeouts = []

            def open_request(request, timeout):
                timeouts.append(timeout)
                return Response(b'ok')

            with patch.object(transport.opener, 'open', side_effect=open_request) as opened:
                clock[0] = 590
                self.assertEqual(b'ok', transport.request('/x', companion.read_response))
                self.assertEqual([10], timeouts)
                opened.reset_mock()
                clock[0] = 600
                with self.assertRaisesRegex(companion.DownloadError, 'deadline exceeded'):
                    transport.request('/y', companion.read_response)
                opened.assert_not_called()

    def test_error_body_is_not_read_at_the_deadline(self):
        clock = [0]
        body = Response(b'secondary rate limit')
        def open_request(request, timeout):
            clock[0] = 600
            raise HTTPError(request.full_url, 403, 'limited', {}, body)

        with patch.object(companion.time, 'monotonic', side_effect=lambda: clock[0]):
            transport = companion.Transport('synthetic')
            with patch.object(transport.opener, 'open', side_effect=open_request) as request, \
                    patch.object(body, 'read1', wraps=body.read1) as read, \
                    self.assertRaisesRegex(companion.DownloadError, 'deadline exceeded'):
                transport.request('/x', companion.read_response)
            request.assert_called_once()
            read.assert_not_called()

    def test_error_body_stops_between_fragments_at_the_deadline(self):
        clock = [0]
        body = Response(b'')
        error = HTTPError('https://api.github.com/x', 403, 'limited', {}, body)

        def first_fragment(size):
            clock[0] = 600
            return b'secondary '

        with patch.object(companion.time, 'monotonic', side_effect=lambda: clock[0]), \
                patch.object(body, 'read1', side_effect=first_fragment) as read:
            transport = companion.Transport('synthetic')
            with patch.object(transport.opener, 'open', side_effect=error) as request, \
                    self.assertRaisesRegex(companion.DownloadError, 'deadline exceeded'):
                transport.request('/x', companion.read_response)
            request.assert_called_once()
            read.assert_called_once_with(8192)

    def test_http_retry_status_boundaries(self):
        for status, retryable in ((407, False), (408, True), (409, False), (499, False),
                                  (500, True), (599, True), (600, False)):
            clock = [0]
            error = HTTPError('https://api.github.com/x', status, 'error', {}, Response(b'error'))
            with self.subTest(status=status), patch.object(companion.time, 'monotonic', side_effect=lambda: clock[0]), \
                    patch.object(companion.time, 'sleep', side_effect=lambda delay: clock.__setitem__(0, clock[0] + delay)):
                transport = companion.Transport('synthetic')
                with patch.object(transport.opener, 'open', side_effect=[error, Response(b'ok')]) as request:
                    if retryable:
                        self.assertEqual(transport.request('/x', companion.read_response), b'ok')
                        self.assertEqual(request.call_count, 2)
                    else:
                        with self.assertRaisesRegex(companion.DownloadError, f'HTTP {status}'):
                            transport.request('/x', companion.read_response)
                        request.assert_called_once()

    def test_request_timeout_uses_smaller_of_15_seconds_and_remaining_time(self):
        for remaining, expected in ((14.99, 14.99), (15, 15), (15.01, 15)):
            with self.subTest(remaining=remaining), patch.object(companion.time, 'monotonic', return_value=0):
                transport = companion.Transport('synthetic')
                transport.deadline = remaining
                with patch.object(transport.opener, 'open', return_value=Response(b'ok')) as request:
                    self.assertEqual(transport.request('/x', companion.read_response), b'ok')
                    self.assertEqual(request.call_args.kwargs['timeout'], expected)

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
                patch.object(companion, 'catalog', return_value=(rows, set())), \
                patch.object(companion, 'download', side_effect=effects) as download, \
                patch.object(companion.sys, 'stderr', io.StringIO()):
            self.assertEqual(companion.main(), 1)
        self.assertEqual([call.args[2] for call in download.call_args_list], ['first', 'second'])



class RecoveryDownloadTests(unittest.TestCase):
    commit = 'a' * 40
    layout = {'coordinator': {'a': 'A.cs', 'b': 'B.cs'}, 'companion': {'d': 'D.cs', 'e': 'E.cs'}}

    def manifest(self, schema):
        run = lambda rid, attempt, role, created: {  # noqa: E731
            'id': rid, 'attempt': attempt, 'path': companion.c5_campaign.PATHS[role], 'created_at': created}
        if schema == 1:
            runs = {'coordinator': run(11, 1, 'coordinator', '2026-11-20T00:10:00Z'),
                    'companion': run(12, 1, 'companion', '2026-11-20T00:20:00Z')}
            origins = {'a': '1', 'b': '1', 'd': '1', 'e': '1'}
        else:
            runs = {'coordinator': {**run(11, 2, 'coordinator', '2026-11-20T00:10:00Z'),
                                    'first_created_at': '2026-11-20T00:00:00Z'},
                    'companion': {**run(12, 2, 'companion', '2026-11-20T00:20:00Z'),
                                  'first_created_at': '2026-11-20T00:05:00Z'}}
            # The companion lost only d: e keeps its attempt 1 evidence although the run is at attempt 2.
            origins = {'a': '1', 'b': '2', 'd': '2', 'e': '1'}
        targets = {'a': 'A.cs', 'b': 'B.cs', 'd': 'D.cs', 'e': 'E.cs'}
        ids = {'a': 11, 'b': 11, 'd': 12, 'e': 12}
        return {'schema': schema, 'repository': 'owner/repo', 'commit': self.commit, 'runs': runs,
                'shards': {s: {'target': targets[s], 'run_id': str(ids[s]), 'run_attempt': origins[s]}
                           for s in targets},
                'incomplete_reasons': []}

    def drive(self, manifest, names):
        data = archive([('receipt.json', '{}')])
        rows = {11: [], 12: []}
        number = 0
        for rid, name in names:
            number += 1
            rows[rid].append({**artifact(name, number, data), 'workflow_run': {'id': rid, 'head_sha': self.commit}})
        requests, runs = [], {}
        for role in ('coordinator', 'companion'):
            identity = manifest['runs'][role]
            runs[identity['id']] = {
                'id': identity['id'], 'run_attempt': identity['attempt'], 'path': companion.c5_campaign.PATHS[role],
                'event': 'workflow_dispatch', 'head_branch': 'main', 'head_sha': self.commit,
                'repository': {'full_name': 'owner/repo'}, 'head_repository': {'full_name': 'owner/repo'},
                'created_at': identity['created_at']}
        by_id = {row['id']: row for listing in rows.values() for row in listing}

        class Fake:
            def request(self, path, consume):
                requests.append(path)
                if '/artifacts?' in path:
                    rid = int(path.split('/runs/')[1].split('/')[0])
                    return json.dumps({'total_count': len(rows[rid]), 'artifacts': rows[rid]}).encode()
                if path.endswith('/zip'):
                    return consume(Response(data), float('inf'))
                rid = int(path.split('/runs/')[1].split('/')[0])
                return json.dumps(runs[rid]).encode()

        with tempfile.TemporaryDirectory() as root:
            command = ['downloader', '--repository', 'owner/repo', '--commit', self.commit,
                       '--manifest', 'unused.json', '--output-dir', root]
            stderr, stdout = io.StringIO(), io.StringIO()
            with patch.object(companion.sys, 'argv', command), patch.dict(companion.os.environ, {'GH_TOKEN': 'synthetic'}), \
                    patch.object(companion.Path, 'read_bytes', return_value=json.dumps(manifest).encode()), \
                    patch.object(companion.c5_campaign, 'matrices', return_value=self.layout), \
                    patch.object(companion.c5_campaign.c5_workflow, 'SHARDS', {'a': 'A.cs', 'b': 'B.cs', 'd': 'D.cs', 'e': 'E.cs'}), \
                    patch.object(companion, 'Transport', return_value=Fake()), \
                    patch.object(companion.sys, 'stderr', stderr), patch.object(companion.sys, 'stdout', stdout):
                code = companion.main()
            downloaded = sorted(path.name for path in Path(root).iterdir())
        zips = [int(path.split('/artifacts/')[1].split('/')[0]) for path in requests if path.endswith('/zip')]
        return code, requests, downloaded, [by_id[number]['name'] for number in zips], stderr.getvalue()

    catalogue = [(11, 'c5-mutation-11-1-a'), (11, 'c5-mutation-11-1-b'), (11, 'c5-mutation-11-2-b'),
                 (11, 'c5-logs-11-1-a'), (11, 'c5-summary-11-2'), (11, 'c5-mutation-11-1-logs-a'),
                 (12, 'c5-mutation-12-1-d'), (12, 'c5-mutation-12-2-d'), (12, 'c5-logs-12-2-d'),
                 (12, 'c5-mutation-12-1-e'), (12, 'c5-mutation-12-2-e')]

    def test_schema_two_downloads_exact_origins(self):
        code, _, downloaded, names, _ = self.drive(self.manifest(2), self.catalogue)
        self.assertEqual(0, code)
        # The companion's re-run shard at its origin and the coordinator's attempt 1 shard; never the
        # coordinator's own attempt 2 archive (its download step fetches it), an older archive of a re-run
        # shard or a log archive.
        self.assertEqual(['c5-mutation-11-1-a', 'c5-mutation-12-1-e', 'c5-mutation-12-2-d'], downloaded)
        self.assertEqual(downloaded, sorted(names))

    def test_schema_one_unchanged(self):
        code, requests, downloaded, names, _ = self.drive(
            self.manifest(1), [(11, 'c5-mutation-11-1-a'), (12, 'c5-mutation-12-1-d'), (12, 'c5-mutation-12-1-e'),
                               (12, 'c5-logs-12-1-d')])
        expected = ['c5-mutation-12-1-d', 'c5-mutation-12-1-e']
        self.assertEqual((0, expected, expected), (code, downloaded, names))
        self.assertEqual(4, len(requests))
        self.assertTrue(all('/runs/12/' in path or '/artifacts/' in path for path in requests))

    def test_schema_one_still_rejects_an_unexpected_archive_of_its_attempt(self):
        code, _, downloaded, _, message = self.drive(
            self.manifest(1), [(12, 'c5-mutation-12-1-d'), (12, 'c5-mutation-12-1-e'), (12, 'c5-mutation-12-1-foreign')])
        self.assertEqual((1, []), (code, downloaded))
        self.assertIn('foreign companion artifact', message)

    def test_schema_two_missing_name_reported(self):
        names = [row for row in self.catalogue if row[1] != 'c5-mutation-12-2-d']
        code, _, downloaded, _, message = self.drive(self.manifest(2), names)
        self.assertEqual((1, ['c5-mutation-11-1-a', 'c5-mutation-12-1-e']), (code, downloaded))
        self.assertIn('missing companion artifacts: c5-mutation-12-2-d', message)

    def test_schema_two_duplicate_expected_name_rejected(self):
        code, _, downloaded, _, message = self.drive(self.manifest(2), [*self.catalogue, (11, 'c5-mutation-11-1-a')])
        self.assertEqual((1, []), (code, downloaded))
        self.assertIn('duplicate', message)


if __name__ == '__main__':
    unittest.main()

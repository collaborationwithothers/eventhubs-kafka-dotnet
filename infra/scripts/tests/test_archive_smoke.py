import hashlib
import io
import ssl
import importlib.util
import json
import os
import signal
import subprocess
import sys
from pathlib import Path
import tempfile
import time
import unittest
from unittest.mock import MagicMock, patch

PATH = Path(__file__).resolve().parents[1] / 'archive' / 'smoke.py'
spec = importlib.util.spec_from_file_location('archive_smoke', PATH)
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)


class ArchiveSmokeTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.private = Path(self.tmp.name) / 'private'; self.private.mkdir()
        self.public = Path(self.tmp.name) / 'public'; self.public.mkdir()
        self.sha = 'a' * 40
        self.ident = {'github_run_id': '123', 'github_run_attempt': 1, 'run_id': 'a01-123-1', 'phase': 'probe', 'source_commit': self.sha}
        self.context = (self.private, self.public, 'archive123.blob.core.windows.net', 'source123.blob.core.windows.net', self.ident, 'A01', 'C0')
        self.objects = {}
        self.calls = []
        self.marker_failure = False

    def fake(self, args, deadline, cap=smoke.MIB):
        action = args[3]; account = args[args.index('--account-name') + 1]
        container = args[args.index('--container-name') + 1]; name = args[args.index('--name') + 1]
        key = (account, container, name); self.calls.append((action, key, args))
        if action == 'upload':
            if self.marker_failure and name.endswith('archive-completion.json'):
                return 1, b'', b'transport failure'
            if key in self.objects: return 1, b'', b'ErrorCode: BlobAlreadyExists'
            self.assertEqual(args[args.index('--if-none-match') + 1], '*')
            self.objects[key] = Path(args[args.index('--file') + 1]).read_bytes()
            return 0, b'', b''
        if action == 'show':
            if key not in self.objects: return 1, b'', b'ErrorCode: BlobNotFound'
            return 0, json.dumps({'properties': {'contentLength': len(self.objects[key]), 'etag': '"etag"'}}).encode(), b''
        if action == 'download':
            self.assertEqual(args[args.index('--if-match') + 1], '"etag"')
            Path(args[args.index('--file') + 1]).write_bytes(self.objects[key])
            return 0, b'', b''
        self.fail(action)

    def invoke(self):
        with patch.object(smoke, 'command', self.fake):
            return smoke.run_probe(self.context, float('inf'))

    def test_success_binds_literal_fixture_and_marker(self):
        self.assertEqual(self.invoke(), 0)
        folder = self.public / 'archive'
        canonical = (folder / 'archive-manifest.json').read_bytes()
        manifest = json.loads(canonical)
        receipt = json.loads((folder / 'archive-receipt.json').read_bytes())
        literal = b'Parchwell archive readiness synthetic fixture v1\n'
        self.assertEqual(manifest['objects'][0]['sha256'], hashlib.sha256(literal).hexdigest())
        self.assertEqual(manifest['totals'], {'expected_objects': 1, 'verified_objects': 1, 'verified_bytes': len(literal)})
        self.assertIsNone(manifest['completion'])
        self.assertEqual(receipt['manifest']['sha256'], hashlib.sha256(canonical).hexdigest())
        marker_bytes = self.objects[('archive123', 'run-evidence', receipt['completion']['name'])]
        self.assertEqual(receipt['completion']['sha256'], hashlib.sha256(marker_bytes).hexdigest())
        self.assertEqual(json.loads(marker_bytes)['manifest'], receipt['manifest'])
        self.assertEqual(self.calls[-3][1][2].split('/')[-1], 'archive-completion.json')
        self.assertFalse(manifest['run']['scientific_admissible'])

    def test_marker_failure_keeps_verified_complete_manifest_binding(self):
        self.marker_failure = True
        self.assertEqual(self.invoke(), 1)
        folder = self.public / 'archive'
        manifest = json.loads((folder / 'archive-manifest.json').read_bytes())
        receipt = json.loads((folder / 'archive-receipt.json').read_bytes())
        self.assertTrue(manifest['archive_complete'])
        self.assertFalse(receipt['archive_complete'])
        self.assertIsNone(receipt['completion'])
        self.assertEqual(receipt['manifest']['sha256'], hashlib.sha256((folder / 'archive-manifest.json').read_bytes()).hexdigest())
        self.assertEqual(receipt['gaps'][0]['code'], 'transport')

    def test_digest_stream_limits_and_symlink(self):
        path = self.private / 'data'; path.write_bytes(b'abc')
        self.assertEqual(smoke.digest(path, 3, 3), hashlib.sha256(b'abc').hexdigest())
        for size, maximum in ((2, 3), (4, 4), (3, 2)):
            with self.assertRaises(smoke.Gap): smoke.digest(path, size, maximum)
        link = self.private / 'link'; link.symlink_to(path)
        with self.assertRaises(OSError): smoke.digest(link, 3)

    def test_invalid_json_and_url(self):
        p = self.private / 'bad'; p.write_text('{"a":1,"a":2}')
        with self.assertRaises(smoke.Invalid): smoke.read_json(p)
        for value in ('https://good.blob.core.windows.net/?sig=secret', 'http://good.blob.core.windows.net', 'https://good.blob.core.windows.net.evil', 'https://user@good.blob.core.windows.net'):
            with self.assertRaises(smoke.Invalid): smoke.endpoint(value)

    def test_actual_wrapper_timeout_reaps_fake_az_process_group(self):
        session = self.private / 'session'; session.mkdir(mode=0o700)
        (session / 'freshness.json').write_text(json.dumps({'request_started_monotonic': time.monotonic(), 'assertion_exp': int(time.time()) + 600}))
        pidfile = self.private / 'child.pid'
        fake = self.private / 'az-real'
        fake.write_text("#!/usr/bin/env python3\nimport subprocess, sys, time\np = subprocess.Popen(['sleep', '30'])\nopen(" + repr(str(pidfile)) + ", 'w').write(str(p.pid))\nprint('child ready', file=sys.stderr, flush=True)\ntime.sleep(30)\n")
        fake.chmod(0o700)
        wrapper = PATH.parents[3] / 'scripts' / 'azure-cli-session' / 'az'
        uid = '11111111-1111-1111-1111-111111111111'
        env = {'GITHUB_ACTIONS': 'true', 'GITHUB_REF': 'refs/heads/main', 'PARCHWELL_REAL_AZ': str(fake), 'PARCHWELL_AZ_SESSION_DIRECTORY': str(session), 'AZURE_CLIENT_ID': uid, 'AZURE_TENANT_ID': uid, 'AZURE_SUBSCRIPTION_ID': uid}
        real_monotonic = time.monotonic
        deadline = real_monotonic() + 10
        def expire_after_child_starts():
            return deadline + 1 if pidfile.exists() else real_monotonic()
        with patch.dict(os.environ, env), patch.object(smoke.time, 'monotonic', side_effect=expire_after_child_starts):
            with self.assertRaises(smoke.Gap) as caught:
                smoke.command([str(wrapper), 'storage', 'blob', 'show'], deadline)
        self.assertEqual(caught.exception.code, 'deadline')
        pid = int(pidfile.read_text())
        proc = Path('/proc') / str(pid) / 'stat'
        self.assertTrue(not proc.exists() or proc.read_text().rsplit(')', 1)[1].split()[0] == 'Z')

    def test_wrapper_serializes_until_interrupted_descendants_are_stopped(self):
        session = self.private / 'session'; session.mkdir(mode=0o700)
        (session / 'freshness.json').write_text(json.dumps({'request_started_monotonic': time.monotonic(), 'assertion_exp': int(time.time()) + 600}))
        pidfile = self.private / 'child.pid'; result = self.private / 'result'
        fake = self.private / 'az-real'
        fake.write_text("#!/usr/bin/env python3\nimport os, pathlib, subprocess, sys, time\npidfile = pathlib.Path(" + repr(str(pidfile)) + ")\nresult = pathlib.Path(" + repr(str(result)) + ")\nif sys.argv[-1] == 'first':\n p = subprocess.Popen([sys.executable, '-c', 'import signal,time; signal.signal(signal.SIGTERM, signal.SIG_IGN); time.sleep(30)'])\n pidfile.write_text(str(p.pid))\n time.sleep(30)\nelse:\n p = pathlib.Path('/proc') / pidfile.read_text() / 'stat'\n result.write_text('stopped' if not p.exists() or p.read_text().rsplit(')',1)[1].split()[0] == 'Z' else 'running')\n")
        fake.chmod(0o700)
        wrapper = PATH.parents[3] / 'scripts' / 'azure-cli-session' / 'az'
        uid = '11111111-1111-1111-1111-111111111111'
        env = dict(os.environ, GITHUB_ACTIONS='true', GITHUB_REF='refs/heads/main', PARCHWELL_REAL_AZ=str(fake), PARCHWELL_AZ_SESSION_DIRECTORY=str(session), AZURE_CLIENT_ID=uid, AZURE_TENANT_ID=uid, AZURE_SUBSCRIPTION_ID=uid)
        first = subprocess.Popen([str(wrapper), 'storage', 'first'], env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            until = time.monotonic() + 5
            while not pidfile.exists() and time.monotonic() < until: time.sleep(.01)
            self.assertTrue(pidfile.exists())
            first.send_signal(signal.SIGTERM)
            second = subprocess.run([str(wrapper), 'storage', 'second'], env=env, capture_output=True, timeout=8)
            self.assertEqual(second.returncode, 0)
            self.assertEqual(result.read_text(), 'stopped')
            first.wait(timeout=5)
        finally:
            if first.poll() is None: first.kill(); first.wait()

    def test_sigterm_cancellation_reaps_actual_wrapper_child(self):
        session = self.private / 'session'; session.mkdir(mode=0o700)
        (session / 'freshness.json').write_text(json.dumps({'request_started_monotonic': time.monotonic(), 'assertion_exp': int(time.time()) + 600}))
        pidfile = self.private / 'child.pid'; fake = self.private / 'az-real'
        fake.write_text("#!/usr/bin/env python3\nimport subprocess,time\np = subprocess.Popen(['sleep', '30'])\nopen(" + repr(str(pidfile)) + ", 'w').write(str(p.pid))\ntime.sleep(30)\n")
        fake.chmod(0o700)
        wrapper = PATH.parents[3] / 'scripts' / 'azure-cli-session' / 'az'
        uid = '11111111-1111-1111-1111-111111111111'
        env = dict(os.environ, GITHUB_ACTIONS='true', GITHUB_REF='refs/heads/main', PARCHWELL_REAL_AZ=str(fake), PARCHWELL_AZ_SESSION_DIRECTORY=str(session), AZURE_CLIENT_ID=uid, AZURE_TENANT_ID=uid, AZURE_SUBSCRIPTION_ID=uid)
        launcher = "import importlib.util,signal,time,sys; s=importlib.util.spec_from_file_location('probe',sys.argv[1]); m=importlib.util.module_from_spec(s); s.loader.exec_module(m); signal.signal(signal.SIGTERM,lambda *_: (_ for _ in ()).throw(KeyboardInterrupt()));\ntry: m.command([sys.argv[2],'storage','first'],time.monotonic()+30)\nexcept m.Gap as e: sys.exit(0 if e.code=='interrupted' else 3)"
        proc = subprocess.Popen([sys.executable, '-c', launcher, str(PATH), str(wrapper)], env=env)
        try:
            until = time.monotonic() + 5
            while not pidfile.exists() and time.monotonic() < until: time.sleep(.01)
            self.assertTrue(pidfile.exists()); proc.send_signal(signal.SIGTERM)
            self.assertEqual(proc.wait(timeout=8), 0)
            child = Path('/proc') / pidfile.read_text() / 'stat'
            self.assertTrue(not child.exists() or child.read_text().rsplit(')', 1)[1].split()[0] == 'Z')
        finally:
            if proc.poll() is None: proc.kill(); proc.wait()

    def test_wrong_conflict_code_fails_without_completion(self):
        original = self.fake
        def wrong(args, deadline, cap=smoke.MIB):
            result = original(args, deadline, cap)
            if args[3] == 'upload' and result[0] and b'BlobAlreadyExists' in result[2]:
                return 1, b'', b'ErrorCode: AuthenticationFailed'
            return result
        with patch.object(smoke, 'command', wrong):
            self.assertEqual(smoke.run_probe(self.context, float('inf')), 1)
        receipt = json.loads((self.public / 'archive' / 'archive-receipt.json').read_bytes())
        self.assertFalse(receipt['archive_complete'])
        self.assertIsNone(receipt['completion'])
        self.assertEqual(receipt['gaps'][0]['code'], 'destination_conflict')

    def test_source_and_destination_digest_mismatches_leave_no_marker(self):
        for account, code in (('source123', 'source_hash_mismatch'), ('archive123', 'destination_hash_mismatch')):
            with self.subTest(account=account), tempfile.TemporaryDirectory() as directory:
                private = Path(directory) / 'private'; private.mkdir()
                public = Path(directory) / 'public'; public.mkdir()
                context = (private, public, *self.context[2:])
                self.objects = {}; self.calls = []
                original = self.fake
                def corrupt(args, deadline, cap=smoke.MIB):
                    result = original(args, deadline, cap)
                    name = args[args.index('--name') + 1]
                    if args[3] == 'download' and args[args.index('--account-name') + 1] == account and name.endswith('synthetic-fixture.txt'):
                        path = Path(args[args.index('--file') + 1]); data = path.read_bytes()
                        path.write_bytes(bytes([data[0] ^ 1]) + data[1:])
                    return result
                with patch.object(smoke, 'command', corrupt):
                    self.assertEqual(smoke.run_probe(context, float('inf')), 1)
                receipt = json.loads((public / 'archive' / 'archive-receipt.json').read_bytes())
                self.assertIsNone(receipt['completion'])
                self.assertEqual(receipt['gaps'][0]['code'], code)
                self.assertFalse(any(x[1][2].endswith('archive-completion.json') for x in self.calls))
                self.assertEqual(list((private / 'archive').iterdir()), [])

    def test_deadline_at_marker_readback_never_authorizes_success(self):
        for when, expected in ((9.999, 0), (10, 1), (10.001, 1)):
            with self.subTest(when=when), tempfile.TemporaryDirectory() as directory:
                private = Path(directory) / 'private'; private.mkdir()
                public = Path(directory) / 'public'; public.mkdir()
                context = (private, public, *self.context[2:])
                self.objects = {}; self.calls = []; now = [0.0]
                original = self.fake
                def advance(args, deadline, cap=smoke.MIB):
                    result = original(args, deadline, cap)
                    if args[3] == 'download' and args[args.index('--name') + 1].endswith('archive-completion.json'):
                        now[0] = when
                    return result
                with patch.object(smoke, 'command', advance):
                    self.assertEqual(smoke.run_probe(context, 10, clock=lambda: now[0]), expected)
                receipt = json.loads((public / 'archive' / 'archive-receipt.json').read_bytes())
                self.assertEqual(receipt['archive_complete'], expected == 0)
                if expected:
                    self.assertIsNone(receipt['completion'])
                    self.assertEqual(receipt['gaps'][-1]['code'], 'deadline')

    def test_output_limit_terminates_process_without_capturing_unbounded_output(self):
        with self.assertRaises(smoke.Gap) as caught:
            smoke.command([sys.executable, '-c', 'import sys,time; sys.stdout.write("x"*200000); sys.stdout.flush(); time.sleep(30)'], time.monotonic() + 5, cap=1024)
        self.assertEqual(caught.exception.code, 'transport')

    def test_configuration_validation_precedes_transport(self):
        uuid = '11111111-1111-1111-1111-111111111111'
        rid = '/subscriptions/' + uuid + '/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/archive123'
        archive = {'storage_account_id': rid, 'account_name': 'archive123', 'blob_service_uri': 'https://archive123.blob.core.windows.net', 'container_name': 'run-evidence', 'container_id': rid + '/blobServices/default/containers/run-evidence', 'retention_days': 90}
        phase = {'run_id': 'a01-123-1', 'source_commit': self.sha, 'scenario': 'A01', 'phase': 'probe', 'configuration': 'C0'}
        env = {'PARCHWELL_PRIVATE_DIRECTORY': str(self.private), 'PARCHWELL_ARTIFACT_DIRECTORY': str(self.public), 'PARCHWELL_RUN_ID': 'a01-123-1', 'GITHUB_RUN_ID': '123', 'GITHUB_RUN_ATTEMPT': '1', 'GITHUB_SHA': self.sha}
        def write():
            (self.private / 'phase.json').write_text(json.dumps(phase))
            (self.private / 'persistent-output.json').write_text(json.dumps({'evidence_archive': {'value': archive}}))
        with patch.dict(os.environ, env), patch.object(smoke, 'command') as call:
            write(); self.assertEqual(smoke.config(False)[4], self.ident)
            for key, bad in [('retention_days', True), ('retention_days', '90'), ('container_name', '../run-evidence'), ('storage_account_id', rid.replace(uuid, '---'))]:
                saved = archive[key]; archive[key] = bad; write()
                with self.assertRaises(smoke.Invalid): smoke.config(False)
                archive[key] = saved
            for key, bad in [('run_id', 'a01-123-2'), ('source_commit', 'b' * 40), ('scenario', 'B10'), ('phase', '../probe')]:
                saved = phase[key]; phase[key] = bad; write()
                with self.assertRaises(smoke.Invalid): smoke.config(False)
                phase[key] = saved
            write()
            (self.private / 'ephemeral-output.json').write_text(json.dumps({'blob': {'value': {'account_url': 'https://archive123.blob.core.windows.net', 'containers': {'archive_smoke_source': 'archive-smoke-source'}}}}))
            with self.assertRaises(smoke.Invalid): smoke.config(True)
            call.assert_not_called()

    def test_short_downloads_use_source_and_destination_length_gaps(self):
        for account, code in (('source123', 'source_length_mismatch'), ('archive123', 'destination_length_mismatch')):
            with self.subTest(account=account), tempfile.TemporaryDirectory() as directory:
                private = Path(directory) / 'private'; private.mkdir()
                public = Path(directory) / 'public'; public.mkdir()
                self.objects = {}; self.calls = []; original = self.fake
                def short(args, deadline, cap=smoke.MIB):
                    result = original(args, deadline, cap)
                    if args[3] == 'download' and args[args.index('--account-name') + 1] == account and args[args.index('--name') + 1].endswith('synthetic-fixture.txt'):
                        path = Path(args[args.index('--file') + 1]); path.write_bytes(path.read_bytes()[:-1])
                    return result
                with patch.object(smoke, 'command', short):
                    self.assertEqual(smoke.run_probe((private, public, *self.context[2:]), float('inf')), 1)
                receipt = json.loads((public / 'archive' / 'archive-receipt.json').read_bytes())
                self.assertEqual(receipt['gaps'][0]['code'], code)
                self.assertIsNone(receipt['completion'])
                self.assertEqual(list((private / 'archive').iterdir()), [])

    def test_oversized_control_stops_before_marker(self):
        original = smoke.encoded
        def oversized(value):
            if value.get('kind') == 'run_evidence_archive': return b'x' * (smoke.MIB + 1)
            return original(value)
        with patch.object(smoke, 'encoded', oversized), patch.object(smoke, 'command', self.fake):
            self.assertEqual(smoke.run_probe(self.context, float('inf')), 1)
        receipt = json.loads((self.public / 'archive' / 'archive-receipt.json').read_bytes())
        self.assertEqual(receipt['gaps'][0]['code'], 'manifest_too_large')
        self.assertIsNone(receipt['manifest']); self.assertIsNone(receipt['completion'])
        self.assertFalse(any(x[1][2].endswith('archive-completion.json') for x in self.calls))

    def test_json_depth_and_expected_length_types(self):
        path = self.private / 'json'; path.write_text('[' * 33 + '0' + ']' * 33)
        with self.assertRaises(smoke.Invalid): smoke.read_json(path)
        file = self.private / 'file'; file.write_bytes(b'abc')
        for value in (-1, True, 1.5, 4):
            with self.assertRaises(smoke.Gap): smoke.digest(file, value, 3)
        with self.assertRaises(smoke.Gap) as caught: smoke.digest(file, 2, side='destination')
        self.assertEqual(caught.exception.code, 'destination_length_mismatch')

    def test_diagnostic_fields_are_allowlisted_and_never_copy_secrets(self):
        secret = b'secret-token-private/path?sig=abcdef'
        stderr = (b'DEBUG: Authorization: Bearer ' + secret + b'\n'
                  b'Response status: 403\nErrorCode: AuthorizationPermissionMismatch\n'
                  b"x-ms-request-id: '11111111-2222-3333-4444-555555555555'\n"
                  b'azure_cli_session_stage pid=23 stage=command event=exit elapsed_ms=5 status=1\n')
        data = smoke.command_evidence(1, stderr)
        self.assertEqual(data['category'], 'storage_service')
        self.assertEqual(data['http_status'], 403)
        self.assertEqual(data['service_code'], 'AuthorizationPermissionMismatch')
        self.assertEqual(data['request_id'], '11111111-2222-3333-4444-555555555555')
        self.assertEqual(data['wrapper_stages'][0]['status'], 1)
        self.assertNotIn(secret.decode(), json.dumps(data))
        untrusted = smoke.command_evidence(1, b'ErrorCode: ' + secret + b'\nx-ms-request-id: ' + secret)
        self.assertEqual(untrusted['category'], 'unknown')
        self.assertIsNone(untrusted['service_code'])
        self.assertIsNone(untrusted['request_id'])
        self.assertNotIn(secret.decode(), json.dumps(untrusted))
        with patch.dict(os.environ, {'AZURE_CLIENT_ID': '11111111-2222-3333-4444-555555555555'}):
            self.assertIsNone(smoke.command_evidence(1, stderr)['request_id'])

    def test_diagnostic_classifications_and_ordered_failure_controls(self):
        for stderr, category in ((b'FileNotFoundError: /private/file', 'local_file'),
                                 (b'unrecognized arguments: --secret', 'arguments'),
                                 (b'NameResolutionError', 'dns'), (b'SSLError', 'tls'),
                                 (b'ConnectionError', 'network_or_sdk_transport'),
                                 (b'azure_cli_session_authentication_failed', 'wrapper'),
                                 (b'ErrorCode: ConditionNotMet\nResponse status: 412', 'storage_service')):
            with self.subTest(category=category):
                self.assertEqual(smoke.command_evidence(1, stderr)['category'], category)
        original = self.fake
        def failed(args, deadline, cap=smoke.MIB):
            if args[3] == 'upload' and args[args.index('--account-name') + 1] == 'source123':
                return 1, b'', b'Response status: 403\nErrorCode: AuthorizationFailure\nsecret-token'
            return original(args, deadline, cap)
        with patch.object(smoke, 'command', failed):
            self.assertEqual(smoke.run_probe(self.context, float('inf')), 1)
        report = json.loads((self.public / 'archive' / 'archive-smoke.json').read_bytes())
        self.assertFalse(report['archive_complete'])
        self.assertEqual([(d['object'], d['operation']) for d in report['diagnostics']],
                         [('fixture', 'upload'), ('manifest', 'upload'), ('manifest', 'show'), ('manifest', 'download')])
        self.assertEqual(report['diagnostics'][0]['service_code'], 'AuthorizationFailure')
        self.assertEqual(report['diagnostics'][1]['outcome'], 'success')
        self.assertNotIn('secret-token', json.dumps(report))

    def test_diagnostic_interruption_and_output_limit_recorded(self):
        for code, boundary in (('interrupted', 'command interrupted'),
                               ('transport', 'command output limit'),
                               ('deadline', 'command timeout')):
            with self.subTest(boundary=boundary), tempfile.TemporaryDirectory() as directory:
                private = Path(directory) / 'private'; private.mkdir()
                public = Path(directory) / 'public'; public.mkdir()
                def fail(*_args, **_kwargs): raise smoke.Gap(code, boundary)
                with patch.object(smoke, 'command', fail):
                    self.assertEqual(smoke.run_probe((private, public, *self.context[2:]), float('inf')), 1)
                report = json.loads((public / 'archive' / 'archive-smoke.json').read_bytes())
                self.assertFalse(report['archive_complete'])
                self.assertEqual(report['diagnostics'][0]['category'], boundary.replace(' ', '_'))
                self.assertIsNone(report['diagnostics'][0]['return_code'])

    def test_sdk_read_is_independent_bounded_and_secret_free(self):
        token = b'private.secret.token'
        calls = []
        def diagnostic_command(args, deadline, cap=smoke.MIB, input_data=None):
            calls.append((args, input_data))
            if args[1:3] == ['account', 'get-access-token']:
                return 0, json.dumps({'accessToken': token.decode(), 'expires_on': 2000000000, 'tenant': 'secret-tenant'}).encode(), b''
            self.assertNotIn(token.decode(), ' '.join(args))
            self.assertEqual(json.loads(input_data)['token'], token.decode())
            self.assertEqual(json.loads(input_data)['expires_on'], 2000000000)
            return 0, json.dumps({'outcome': 'failure', 'exception_types': ['HttpResponseError'],
                                  'errno': None, 'http_status': 403,
                                  'service_code': 'AuthorizationFailure', 'request_id': None}).encode(), b''
        with patch.object(smoke, 'command', diagnostic_command):
            result = smoke.sdk_diagnostic('/usr/bin/python3', self.context[3], time.monotonic() + 60)
        self.assertEqual(result['outcome'], 'observed')
        self.assertEqual(result['read']['http_status'], 403)
        self.assertNotIn(token.decode(), json.dumps(result))
        self.assertEqual(len(calls), 2)
        with patch.object(smoke, 'command') as call:
            self.assertEqual(smoke.sdk_diagnostic('/usr/bin/python3', self.context[3], time.monotonic() + 1)['outcome'], 'skipped_deadline')
            call.assert_not_called()
        with patch.object(smoke, 'command', side_effect=smoke.Gap('interrupted', 'command interrupted')):
            self.assertEqual(smoke.sdk_diagnostic('/usr/bin/python3', self.context[3], time.monotonic() + 60)['outcome'], 'interrupted')

    def test_sdk_helper_rejects_untrusted_output_and_stdin_is_bounded(self):
        def bad(args, deadline, cap=smoke.MIB, input_data=None):
            if input_data is None: return 0, b'{\"accessToken\":\"secret.token\",\"expires_on\":2000000000}', b''
            return 0, b'{"outcome":"secret.token"}', b'secret.token'
        with patch.object(smoke, 'command', bad):
            result = smoke.sdk_diagnostic('/usr/bin/python3', self.context[3], time.monotonic() + 60)
        self.assertEqual(result['outcome'], 'unavailable')
        self.assertNotIn('secret.token', json.dumps(result))
        with self.assertRaises(smoke.Gap) as caught:
            smoke.command([sys.executable, '-c', 'import time; time.sleep(30)'],
                          time.monotonic() + .2, input_data=b'x' * 1000000)
        self.assertEqual(caught.exception.code, 'deadline')

    def test_network_observation_is_separate_and_rejects_untrusted_fields(self):
        observation = {'dns': 'success', 'tcp': 'success', 'tls': 'failure', 'errno': None, 'tls_reason': 'CERTIFICATE_VERIFY_FAILED', 'tls_verify_code': 20, 'tls_failure_kind': 'certificate_verification'}
        with patch.object(smoke, 'command', return_value=(0, json.dumps(observation).encode(), b'secret')):
            result = smoke.network_diagnostic('/usr/bin/python3', self.context[3], time.monotonic() + 60)
        self.assertEqual(result, {'outcome': 'observed', 'observation': observation})
        observation['tls'] = 'secret-token'
        with patch.object(smoke, 'command', return_value=(0, json.dumps(observation).encode(), b'')):
            result = smoke.network_diagnostic('/usr/bin/python3', self.context[3], time.monotonic() + 60)
        self.assertEqual(result['outcome'], 'unavailable')
        self.assertNotIn('secret-token', json.dumps(result))
        with patch.object(smoke, 'command') as call:
            self.assertEqual(smoke.network_diagnostic('/usr/bin/python3', self.context[3], time.monotonic() + 1)['outcome'], 'skipped_deadline')
            call.assert_not_called()

    def test_last_response_and_terminal_exception_take_precedence(self):
        stderr = (b'DEBUG: azure.core.pipeline.policies.http_logging_policy:Response status: 401\n'
                  b"DEBUG: azure.core.pipeline.policies.http_logging_policy:    'x-ms-request-id': '11111111-2222-3333-4444-555555555555'\n"
                  b'DEBUG: azure.core.pipeline.policies.http_logging_policy:Response status: 403\n'
                  b"DEBUG: azure.core.pipeline.policies.http_logging_policy:    'x-ms-request-id': 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'\n"
                  b'ErrorCode: AuthenticationFailed\nErrorCode: AuthorizationFailure\n'
                  b'ValueError: earlier\nazure.core.exceptions.HttpResponseError: terminal\n')
        result = smoke.command_evidence(1, stderr)
        self.assertEqual(result['http_status'], 403)
        self.assertEqual(result['request_id'], 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee')
        self.assertEqual(result['service_code'], 'AuthorizationFailure')
        self.assertEqual(result['exception_type'], 'HttpResponseError')
        result = smoke.command_evidence(1, stderr + b'Response status: 500\n')
        self.assertEqual(result['http_status'], 500)
        self.assertIsNone(result['request_id'])
        rows = b'azure_cli_session_stage pid=1 stage=login event=auth_code elapsed_ms=4 aadsts=AADSTS70021\n'
        self.assertEqual(smoke.command_evidence(1, rows)['wrapper_stages'][0]['aadsts'], 'AADSTS70021')
        self.assertEqual(smoke.command_evidence(1, rows.replace(b'AADSTS70021', b'AADSTS123456'))['wrapper_stages'], [])

    def test_failure_target_is_diagnosed_first_for_both_targets(self):
        for failed_account, first in (('source123', 'source'), ('archive123', 'archive')):
            with self.subTest(target=first), tempfile.TemporaryDirectory() as directory:
                private = Path(directory) / 'private'; private.mkdir()
                public = Path(directory) / 'public'; public.mkdir()
                self.objects = {}; self.calls = []
                original = self.fake
                def fail(args, deadline, cap=smoke.MIB):
                    if args[3] == 'upload' and args[args.index('--account-name') + 1] == failed_account and args[args.index('--name') + 1].endswith('synthetic-fixture.txt'):
                        return 1, b'', b'ErrorCode: AuthorizationFailure'
                    return original(args, deadline, cap)
                seen = []
                def sdk(python, host, deadline, container):
                    seen.append((host, container)); return {'outcome': 'unavailable', 'token': None, 'read': None}
                with patch.object(smoke, 'command', fail), patch.object(smoke, 'sdk_diagnostic', sdk), patch.object(smoke, 'network_diagnostic', return_value={'outcome': 'unavailable', 'observation': None}):
                    self.assertEqual(smoke.run_probe((private, public, *self.context[2:]), float('inf')), 1)
                self.assertEqual(seen[0][0], self.context[3] if first == 'source' else self.context[2])
                report = json.loads((public / 'archive' / 'archive-smoke.json').read_bytes())
                self.assertEqual(set(report['sdk_diagnostic']), {'source', 'archive'})
                self.assertEqual(set(report['network_diagnostic']), {'source', 'archive'})

    def test_partial_stderr_survives_timeout_and_output_limit_without_public_secret(self):
        line = 'azure_cli_session_stage pid=3 stage=authentication event=start elapsed_ms=7\n'
        for body, cap, deadline, boundary in (("import sys,time; sys.stderr.write(" + repr(line + 'secret-token\n') + "); sys.stderr.flush(); time.sleep(30)", 1024, .2, 'command timeout'),
                                               ("import sys; sys.stderr.write(" + repr(line + 'secret-token\n') + "); sys.stderr.flush(); sys.stdout.write('x'*20000); sys.stdout.flush()", 1024, 5, 'command output limit')):
            with self.subTest(boundary=boundary):
                with self.assertRaises(smoke.Gap) as caught:
                    smoke.command([sys.executable, '-c', body], time.monotonic() + deadline, cap=cap)
                self.assertEqual(caught.exception.boundary, boundary)
                error = caught.exception
                def fail(*args, **kwargs): raise error
                with tempfile.TemporaryDirectory() as directory:
                    private = Path(directory) / 'private'; private.mkdir()
                    public = Path(directory) / 'public'; public.mkdir()
                    with patch.object(smoke, 'command', fail):
                        self.assertEqual(smoke.run_probe((private, public, *self.context[2:]), float('inf')), 1)
                    report = json.loads((public / 'archive' / 'archive-smoke.json').read_bytes())
                    first = report['diagnostics'][0]
                    self.assertEqual(first['wrapper_stages'][0]['stage'], 'authentication')
                    self.assertTrue(first['started_at'].endswith('Z'))
                    self.assertGreaterEqual(first['elapsed_seconds'], 0)
                    self.assertNotIn('secret-token', json.dumps(report))

    def test_expected_negatives_are_labeled_and_unexpected_source_conflict_is_failure(self):
        self.assertEqual(self.invoke(), 0)
        report = json.loads((self.public / 'archive' / 'archive-smoke.json').read_bytes())
        negatives = [entry['expected_negative'] for entry in report['diagnostics'] if entry['expected_negative']]
        self.assertEqual(negatives, ['pre_create_not_found', 'create_conflict'])
        with tempfile.TemporaryDirectory() as directory:
            private = Path(directory) / 'private'; private.mkdir()
            public = Path(directory) / 'public'; public.mkdir()
            original = self.fake; seen = []
            def fail(args, deadline, cap=smoke.MIB):
                if args[3] == 'upload': return 1, b'', b'ErrorCode: BlobAlreadyExists'
                return original(args, deadline, cap)
            def sdk(python, host, deadline, container):
                seen.append(host); return {'outcome': 'unavailable', 'token': None, 'read': None}
            with patch.object(smoke, 'command', fail), patch.object(smoke, 'sdk_diagnostic', sdk), patch.object(smoke, 'network_diagnostic', return_value={'outcome': 'unavailable', 'observation': None}):
                self.assertEqual(smoke.run_probe((private, public, *self.context[2:]), float('inf')), 1)
            report = json.loads((public / 'archive' / 'archive-smoke.json').read_bytes())
            self.assertEqual(seen[0], self.context[3])
            self.assertIsNone(report['diagnostics'][0]['expected_negative'])

    def test_network_helper_attributes_socket_and_tls_failures_without_messages(self):
        spec = importlib.util.spec_from_file_location('network_helper', PATH.with_name('smoke_network_diagnostic.py'))
        helper = importlib.util.module_from_spec(spec); spec.loader.exec_module(helper)
        address = [(2, 1, 6, '', ('192.0.2.1', 443))]
        def run(socket_factory, context_factory):
            output = io.StringIO()
            with patch.object(helper.sys, 'stdin', io.StringIO('{"host":"source123.blob.core.windows.net"}')), patch.object(helper.sys, 'stdout', output), patch.object(helper.socket, 'getaddrinfo', return_value=address), patch.object(helper.socket, 'socket', socket_factory), patch.object(helper.ssl, 'create_default_context', context_factory):
                helper.main()
            self.assertNotIn('secret', output.getvalue())
            return json.loads(output.getvalue())
        result = run(MagicMock(side_effect=OSError(24, 'secret')), MagicMock())
        self.assertEqual((result['dns'], result['tcp'], result['tls']), ('success', 'failure', 'not_attempted'))
        self.assertEqual(result['errno'], 24)
        sock = MagicMock(); sock.__enter__.return_value = sock
        certificate_error = ssl.SSLCertVerificationError('secret')
        certificate_error.verify_code = 20; certificate_error.reason = 'CERTIFICATE_VERIFY_FAILED'
        context = MagicMock(); context.wrap_socket.side_effect = certificate_error
        result = run(MagicMock(return_value=sock), MagicMock(return_value=context))
        self.assertEqual((result['dns'], result['tcp'], result['tls']), ('success', 'success', 'failure'))
        self.assertEqual(result['tls_reason'], 'CERTIFICATE_VERIFY_FAILED')
        self.assertEqual(result['tls_verify_code'], 20)
        self.assertEqual(result['tls_failure_kind'], 'certificate_verification')
        certificate_error.reason = 'secret-invalid-reason'
        result = run(MagicMock(return_value=sock), MagicMock(return_value=context))
        self.assertIsNone(result['tls_reason'])
        result = run(MagicMock(return_value=sock), MagicMock(side_effect=ValueError('secret')))
        self.assertEqual(result['tcp'], 'success')
        self.assertEqual(result['tls_failure_kind'], 'configuration')

    def test_unsupported_versions_fail_closed(self):
        with patch.object(smoke, 'command', return_value=(0, b'{"azure-cli":"2.91.0"}', b'')):
            with self.assertRaises(smoke.Invalid): smoke.preflight(float('inf'))
        for document in (b'[]', b'null', b'true', b'3', b'"unsupported"'):
            with self.subTest(document=document), patch.object(smoke, 'command', return_value=(0, document, b'')):
                with self.assertRaises(smoke.Invalid): smoke.preflight(float('inf'))
        self.assertEqual(smoke.main(['run', 'extra']), 2)

if __name__ == '__main__': unittest.main()

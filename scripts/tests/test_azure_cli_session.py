"""Fixed issue-124 inventory: synthetic assertions and processes; no Azure calls."""
import base64
import contextlib
import fcntl
import importlib.machinery
import importlib.util
import io
import json
import multiprocessing
import os
from pathlib import Path
import selectors
import signal
import stat
import subprocess
import sys
import tempfile
import time
import unittest
from unittest import mock
from urllib import error, parse

ROOT = Path(__file__).resolve().parents[2]
WRAPPER = ROOT / "scripts/azure-cli-session/az"
loader = importlib.machinery.SourceFileLoader("azure_cli_session", str(WRAPPER))
spec = importlib.util.spec_from_loader(loader.name, loader)
session = importlib.util.module_from_spec(spec)
loader.exec_module(session)
EXPORT = "always() && !cancelled() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success'"
DESTROY = "always() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success'"
SETUP = '''set -euo pipefail
umask 077
real_az="$(command -v az)"
test -x "$real_az"
session_dir="$(mktemp -d "$RUNNER_TEMP/a01-oidc.XXXXXX")"
chmod 700 "$session_dir"
test -x "$GITHUB_WORKSPACE/scripts/azure-cli-session/az"
printf 'PARCHWELL_REAL_AZ=%s\\n' "$real_az" >> "$GITHUB_ENV"
printf 'PARCHWELL_AZ_SESSION_DIRECTORY=%s\\n' "$session_dir" >> "$GITHUB_ENV"
printf '%s\\n' "$GITHUB_WORKSPACE/scripts/azure-cli-session" >> "$GITHUB_PATH"
'''
FAKE_CLI = r'''import fcntl, json, os, signal, subprocess, sys, time
from pathlib import Path
root = Path(os.environ['FIXTURE_DIR'])
config = json.loads((root / 'cli.json').read_text())
args = sys.argv[1:]
with (root / 'active').open('a') as guard:
    try:
        fcntl.flock(guard, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        (root / 'overlap').touch()
    with (root / 'calls').open('a') as log:
        log.write(json.dumps(args) + '\n')
    if args == ['check-tree']:
        direct, grandchild = json.loads((root / 'pids').read_text())
        assert not Path('/proc', str(direct)).exists(), 'direct child was not reaped before unlock'
        status = Path('/proc', str(grandchild), 'stat')
        assert not status.exists() or status.read_text().split()[2] == 'Z', 'grandchild survived unlock'
    if args == ['hold'] or args == ['tree']:
        if args == ['tree']:
            read_fd, write_fd = os.pipe()
            child_code = ("import os,signal,time; from pathlib import Path; "
                "signal.signal(signal.SIGTERM,signal.SIG_IGN); "
                "p=Path(os.environ['FIXTURE_DIR'])/'heartbeat'; "
                "p.write_text(str(time.monotonic())); "
                "os.write(" + str(write_fd) + ",b'1'); "
                "exec('while True:\\n p.write_text(str(time.monotonic()))\\n time.sleep(.01)')")
            child = subprocess.Popen([sys.executable, '-c', child_code], pass_fds=(write_fd,))
            os.close(write_fd)
            assert os.read(read_fd, 1) == b'1'
            os.close(read_fd)
            (root / 'pids').write_text(json.dumps([os.getpid(), child.pid]))
            signal.signal(signal.SIGTERM, signal.SIG_IGN)
        with (root / 'ready').open('w') as ready:
            ready.write('ready\n')
        with (root / 'release').open() as release:
            release.read()
    os.write(1, bytes.fromhex(config.get('stdout', '')))
    os.write(2, bytes.fromhex(config.get('stderr', '')))
    sys.exit(config.get('exit', 0))
'''


def token(exp, marker="synthetic"):
    payload = base64.urlsafe_b64encode(json.dumps({"exp": exp, "jti": marker}).encode()).rstrip(b"=").decode()
    return "synthetic." + payload + ".signature"


def body(assertion):
    return json.dumps({"value": assertion}).encode()


def process_worker(env, args, result, waiting=None, budget=None):
    """Only the test process injects dependencies; the shipped CLI has no bypass."""
    os.environ.clear()
    os.environ.update(env)
    if budget is not None:
        session.INVOCATION_SECONDS = budget
    def fetch(*unused):
        with (Path(env['FIXTURE_DIR']) / 'requests').open('a') as stream:
            stream.write('request\n')
        return body(token(int(time.time()) + 600))
    def sleep(seconds):
        if waiting:
            waiting.set()
        time.sleep(seconds)
    result.put(session.main(args, environ=env, fetch=fetch, sleep=sleep))


class Clock:
    def __init__(self):
        self.now = 1000.0
        self.epoch = 2_000_000_000
    def advance(self, seconds):
        self.now += seconds
        self.epoch += seconds


class AzureCliSessionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.private = self.root / 'session'
        self.private.mkdir(mode=0o700)
        self.cli = self.root / 'real-az'
        self.cli.write_text('#!' + sys.executable + '\n' + FAKE_CLI)
        self.cli.chmod(0o700)
        self.configure_cli()
        self.env = {
            'PATH': str(Path(sys.executable).parent) + ':' + os.defpath,
            'FIXTURE_DIR': str(self.root),
            'GITHUB_ACTIONS': 'true', 'GITHUB_REF': 'refs/heads/main',
            'PARCHWELL_REAL_AZ': str(self.cli),
            'PARCHWELL_AZ_SESSION_DIRECTORY': str(self.private),
            'AZURE_CLIENT_ID': '11111111-1111-4111-8111-111111111111',
            'AZURE_TENANT_ID': '22222222-2222-4222-8222-222222222222',
            'AZURE_SUBSCRIPTION_ID': '33333333-3333-4333-8333-333333333333',
            'ACTIONS_ID_TOKEN_REQUEST_URL': 'https://issuer.invalid/token?keep=a%2Bb&audience=old&keep=two',
            'ACTIONS_ID_TOKEN_REQUEST_TOKEN': 'synthetic-bearer-secret',
            'PYTHONDONTWRITEBYTECODE': '1',
        }
        self.clock = Clock()
        self.calls = []
        self.requests = []
        self.diagnostics = ''
        self.context = multiprocessing.get_context('fork')

    @property
    def state(self):
        return self.private / 'freshness.json'

    def configure_cli(self, stdout=b'', stderr=b'', exit_code=0):
        (self.root / 'cli.json').write_text(json.dumps({'stdout': stdout.hex(), 'stderr': stderr.hex(), 'exit': exit_code}))

    def stamp(self, age=0, life=600):
        self.state.write_text(json.dumps({'request_started_monotonic': self.clock.now - age, 'assertion_exp': int(self.clock.epoch + life)}))
        self.state.chmod(0o600)

    def fetch(self, url, headers, timeout):
        self.requests.append((url, headers, timeout))
        return body(token(int(self.clock.epoch) + 600, str(len(self.requests))))

    def launch(self, argv, timeout, quiet):
        self.calls.append((argv, timeout, quiet))
        return 0

    def invoke(self, args=None, **dependencies):
        output = io.StringIO()
        values = dict(environ=self.env, monotonic=lambda: self.clock.now,
                      utc=lambda: self.clock.epoch, sleep=self.clock.advance,
                      fetch=self.fetch, launch=self.launch)
        values.update(dependencies)
        with contextlib.redirect_stderr(output), contextlib.redirect_stdout(output):
            result = session.main(['resource', 'list'] if args is None else args, **values)
        self.diagnostics += output.getvalue()
        return result

    def reset(self):
        self.state.unlink(missing_ok=True)
        self.calls.clear()
        self.requests.clear()
        self.diagnostics = ''

    def real(self, args, **kwargs):
        # A test-owned driver imports the production entrypoint with a fake issuer.
        driver = ("import importlib.machinery,json,os,time; "
                  "m=importlib.machinery.SourceFileLoader('shim',os.environ['FIXTURE_WRAPPER']).load_module(); "
                  "raise SystemExit(m.main(json.loads(os.environ['FIXTURE_ARGS']), "
                  "fetch=lambda *a: os.environ['FIXTURE_BODY'].encode()))")
        env = dict(self.env, FIXTURE_WRAPPER=str(WRAPPER), FIXTURE_ARGS=json.dumps(args),
                   FIXTURE_BODY=body(token(int(time.time()) + 600)).decode())
        return subprocess.run([sys.executable, '-c', driver], env=env, capture_output=True, timeout=5, **kwargs)

    def fixture_calls(self):
        path = self.root / 'calls'
        return [json.loads(line) for line in path.read_text().splitlines()] if path.exists() else []

    def start(self, args, waiting=None, budget=None):
        result = self.context.Queue()
        process = self.context.Process(target=process_worker, args=(self.env, args, result, waiting, budget))
        process.start()
        def cleanup():
            if process.is_alive():
                process.kill()
            process.join(2)
            result.close()
        self.addCleanup(cleanup)
        return process, result

    def barriers(self):
        for name in ('ready', 'release'):
            os.mkfifo(self.root / name)
        fd = os.open(self.root / 'ready', os.O_RDWR | os.O_NONBLOCK)
        self.addCleanup(os.close, fd)
        return fd

    def ready(self, fd):
        with selectors.DefaultSelector() as selector:
            selector.register(fd, selectors.EVENT_READ)
            self.assertTrue(selector.select(3), 'fake CLI never reached its barrier')
        self.assertEqual(b'ready\n', os.read(fd, 100))

    def release(self):
        with (self.root / 'release').open('w') as stream:
            stream.write('continue')

    def finish(self, process, result, expected=0):
        self.assertEqual(expected, result.get(timeout=4))
        process.join(4)
        self.assertEqual(0, process.exitcode)

    def registry_args(self):
        return ['acr', 'repository', 'show', '--name', 'synthetic-registry',
                '--image', 'synthetic-worker:synthetic-tag', '--query', 'digest', '--output', 'tsv']

    def stage_rows(self, text):
        rows = []
        for line in text.splitlines():
            if not line.startswith('azure_cli_session_stage '):
                continue
            self.assertRegex(line, r'^azure_cli_session_stage pid=[0-9]+ stage=[a-z_]+ '
                             r'event=(start|end|exit|timeout|interrupted|error) elapsed_ms=[0-9]+'
                             r'( stage_ms=[0-9]+)?( status=[0-9]+)?$')
            rows.append(dict(field.split('=', 1) for field in line.split()[1:]))
        self.assertTrue(rows, 'registry lookup emitted no stage diagnostics')
        self.assertEqual(1, len({row['pid'] for row in rows}))
        for value in [*self.env.values(), *self.registry_args()[4:]]:
            if len(value) > 10:
                self.assertNotIn(value, '\n'.join(line for line in text.splitlines()
                                               if line.startswith('azure_cli_session_stage ')))
        return rows

    def test_registry_diagnostics_separate_auth_and_command(self):
        self.assertEqual(0, self.invoke(self.registry_args()))
        rows = self.stage_rows(self.diagnostics)
        self.assertEqual(['lock', 'authentication', 'token_request', 'login', 'account_select', 'command'],
                         [row['stage'] for row in rows if row['event'] == 'start'])
        self.assertEqual(['0', '0', '0'], [row['status'] for row in rows if row['event'] == 'exit'])
        self.assertEqual(1, len(self.requests))
        self.assertEqual(3, len(self.calls))
        self.assertEqual([self.cli.as_posix(), *self.registry_args()], self.calls[-1][0])
        self.assertEqual([True, True, False], [call[2] for call in self.calls])

    def test_registry_diagnostics_identify_lock_timeout(self):
        with (self.private / 'lock').open('a') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX)
            self.assertEqual(124, self.invoke(self.registry_args()))
        rows = self.stage_rows(self.diagnostics)
        self.assertEqual([('lock', 'start'), ('lock', 'timeout')],
                         [(row['stage'], row['event']) for row in rows])
        self.assertGreaterEqual(int(rows[-1]['stage_ms']), 60000)
        self.assertEqual([], self.calls)
        self.assertEqual([], self.requests)
        self.assertIn('azure_cli_session_timeout\n', self.diagnostics)

    def test_registry_diagnostics_identify_auth_failures_without_secrets(self):
        for failed_stage in ('token_request', 'login', 'account_select'):
            for failure in ('timeout', 'error', 'interrupted', 'nonzero'):
                if failed_stage == 'token_request' and failure == 'nonzero':
                    continue
                with self.subTest(stage=failed_stage, failure=failure):
                    self.reset()
                    secret = 'synthetic-sensitive-exception ' + self.env['ACTIONS_ID_TOKEN_REQUEST_TOKEN']
                    def fail():
                        self.clock.advance(0.25)
                        if failure == 'timeout':
                            raise subprocess.TimeoutExpired(secret, 0.25)
                        if failure == 'interrupted':
                            raise session.SessionInterrupted(signal.SIGTERM)
                        if failure == 'error':
                            raise RuntimeError(secret)
                        return 17
                    def fetch(*args):
                        return fail() if failed_stage == 'token_request' else self.fetch(*args)
                    def launch(argv, timeout, quiet):
                        stage = 'login' if argv[1] == 'login' else 'account_select'
                        return fail() if stage == failed_stage else self.launch(argv, timeout, quiet)
                    expected = 124 if failure == 'timeout' else 143 if failure == 'interrupted' else 1
                    self.assertEqual(expected, self.invoke(self.registry_args(), fetch=fetch, launch=launch))
                    rows = self.stage_rows(self.diagnostics)
                    event = 'exit' if failure == 'nonzero' else failure
                    self.assertTrue(any(row['stage'] == failed_stage and row['event'] == event for row in rows))
                    self.assertNotIn('command', [row['stage'] for row in rows])
                    self.assertNotIn(secret, self.diagnostics)
                    self.assertNotIn(self.env['ACTIONS_ID_TOKEN_REQUEST_TOKEN'], self.diagnostics)
                    self.assertFalse(self.state.exists())
                    if failure == 'nonzero':
                        self.assertTrue(any(row.get('status') == '17' for row in rows))

    def test_registry_diagnostics_identify_forwarded_failure_and_cancellation(self):
        for outcome in (1, 124, 'timeout', 'interrupted'):
            with self.subTest(outcome=outcome):
                self.reset()
                self.stamp()
                def launch(argv, timeout, quiet):
                    self.assertFalse(quiet)
                    self.assertEqual(60, timeout)
                    self.clock.advance(0.5)
                    if outcome == 'timeout':
                        raise subprocess.TimeoutExpired('synthetic-private-argv', timeout)
                    if outcome == 'interrupted':
                        raise session.SessionInterrupted(signal.SIGINT)
                    return outcome
                expected = 124 if outcome == 'timeout' else 130 if outcome == 'interrupted' else outcome
                self.assertEqual(expected, self.invoke(self.registry_args(), launch=launch))
                rows = self.stage_rows(self.diagnostics)
                self.assertEqual(['lock', 'command'], [r['stage'] for r in rows if r['event'] == 'start'])
                self.assertEqual('500', rows[-1]['stage_ms'])
                self.assertEqual([], self.requests)
                self.assertNotIn('synthetic-private-argv', self.diagnostics)

    def test_registry_diagnostics_flush_before_real_command_timeout_and_cleanup(self):
        self.registry_subprocess_diagnostics('timeout')

    def test_registry_diagnostics_flush_before_external_cancellation_and_cleanup(self):
        self.registry_subprocess_diagnostics('cancellation')

    def registry_subprocess_diagnostics(self, termination):
        self.cli.write_text('#!' + sys.executable + '\nimport sys,time\n'
                            'if sys.argv[1:4] == ["acr", "repository", "show"]: time.sleep(30)\n')
        driver = ('import importlib.machinery,json,os,time; '
                  'm=importlib.machinery.SourceFileLoader("shim",os.environ["FIXTURE_WRAPPER"]).load_module(); '
                  'm.INVOCATION_SECONDS=int(os.environ["FIXTURE_BUDGET"]); '
                  'raise SystemExit(m.main(json.loads(os.environ["FIXTURE_ARGS"]), '
                  'fetch=lambda *a: os.environ["FIXTURE_BODY"].encode()))')
        env = dict(self.env, FIXTURE_WRAPPER=str(WRAPPER), FIXTURE_ARGS=json.dumps(self.registry_args()),
                   FIXTURE_BODY=body(token(int(time.time()) + 600)).decode(),
                   FIXTURE_BUDGET='1' if termination == 'timeout' else '60')
        process = subprocess.Popen([sys.executable, '-c', driver], env=env,
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE, bufsize=0)
        def cleanup():
            if process.poll() is None:
                process.terminate()
            process.communicate(timeout=4)
        self.addCleanup(cleanup)
        captured = b''
        with selectors.DefaultSelector() as selector:
            selector.register(process.stderr, selectors.EVENT_READ)
            deadline = time.monotonic() + 3
            while b'stage=command event=start' not in captured:
                self.assertGreater(deadline - time.monotonic(), 0, 'no flushed command marker')
                self.assertTrue(selector.select(max(0, deadline - time.monotonic())))
                chunk = os.read(process.stderr.fileno(), 65536)
                self.assertTrue(chunk, captured.decode())
                captured += chunk
        self.assertIsNone(process.poll(), 'marker only appeared after the process exited')
        if termination == 'cancellation':
            process.send_signal(signal.SIGTERM)
        stdout, rest = process.communicate(timeout=4)
        captured += rest
        self.assertEqual(124 if termination == 'timeout' else 143, process.returncode)
        self.assertEqual(b'', stdout)
        rows = self.stage_rows(captured.decode())
        self.assertEqual(['cleanup', 'command'], [r['stage'] for r in rows[-2:]])
        self.assertEqual(['end', 'timeout' if termination == 'timeout' else 'interrupted'],
                         [r['event'] for r in rows[-2:]])
        self.assertTrue(any(r['stage'] == 'cleanup' and r['event'] == 'start' for r in rows))
        self.assertNotIn(self.env['ACTIONS_ID_TOKEN_REQUEST_TOKEN'], captured.decode())

    def test_registry_diagnostics_preserve_real_digest_stdout_and_cli_stderr(self):
        digest = b'sha256:' + b'a' * 64 + b'\n'
        self.configure_cli(stdout=digest, stderr=b'synthetic-cli-notice\n')
        result = self.real(self.registry_args())
        self.assertEqual(0, result.returncode)
        self.assertEqual(digest, result.stdout)
        self.assertEqual(1, result.stderr.count(b'synthetic-cli-notice\n'))
        rows = self.stage_rows(result.stderr.decode())
        self.assertEqual(['0', '0', '0'], [r['status'] for r in rows if r['event'] == 'exit'])
        self.assertNotIn('cleanup', [r['stage'] for r in rows])

    def test_01_required_context_rejects_before_side_effects(self):
        mutations = [(key, None) for key in ('GITHUB_ACTIONS', 'GITHUB_REF', 'PARCHWELL_REAL_AZ', 'PARCHWELL_AZ_SESSION_DIRECTORY', 'AZURE_CLIENT_ID', 'AZURE_TENANT_ID', 'AZURE_SUBSCRIPTION_ID')]
        mutations += [('GITHUB_ACTIONS', 'false'), ('GITHUB_REF', 'refs/heads/topic'), ('PARCHWELL_REAL_AZ', 'az'), ('PARCHWELL_REAL_AZ', str(WRAPPER)), ('PARCHWELL_REAL_AZ', str(self.root / 'absent')), ('AZURE_CLIENT_ID', 'not-a-uuid')]
        for key, value in mutations:
            with self.subTest(key=key, value=value):
                env = dict(self.env)
                if value is None:
                    env.pop(key)
                else:
                    env[key] = value
                self.assertEqual(1, self.invoke(environ=env))
        self.assertEqual(1, self.invoke([]))
        self.private.chmod(0o755)
        self.assertEqual(1, self.invoke())
        self.assertEqual([], self.requests)
        self.assertEqual([], self.calls)
        self.assertEqual({'azure_cli_session_configuration_invalid'}, set(self.diagnostics.splitlines()))

    def test_02_initial_refresh_order_and_exact_authentication(self):
        self.assertEqual(0, self.invoke(['group', 'delete', '--name', 'synthetic']))
        assertion = token(self.clock.epoch + 600, '1')
        self.assertEqual([str(self.cli), 'login', '--service-principal', '--username', self.env['AZURE_CLIENT_ID'], '--tenant', self.env['AZURE_TENANT_ID'], '--federated-token', assertion, '--output', 'none', '--only-show-errors'], self.calls[0][0])
        self.assertEqual([str(self.cli), 'account', 'set', '--subscription', self.env['AZURE_SUBSCRIPTION_ID'], '--output', 'none', '--only-show-errors'], self.calls[1][0])
        self.assertEqual([str(self.cli), 'group', 'delete', '--name', 'synthetic'], self.calls[2][0])
        self.assertEqual([True, True, False], [call[2] for call in self.calls])
        self.assertEqual([30, 10, 60], [call[1] for call in self.calls])
        self.assertEqual(1, len(self.requests))
        self.assertEqual('', self.diagnostics)

    def test_03_age_boundary(self):
        for age in (119, 120, 121):
            with self.subTest(age=age):
                self.reset()
                self.stamp(age=age)
                self.assertEqual(0, self.invoke())
                self.assertEqual(int(age >= 120), len(self.requests))
                self.assertEqual(1 if age == 119 else 3, len(self.calls))

    def test_04_expiry_headroom_boundary(self):
        for life in (89, 90, 91):
            with self.subTest(life=life):
                self.reset()
                self.stamp(life=life)
                self.assertEqual(0, self.invoke())
                self.assertEqual(int(life == 89), len(self.requests))

    def test_05_invalid_state_cannot_authorize_mutation(self):
        invalid = (None, '{', '{}', '{"request_started_monotonic":NaN,"assertion_exp":2000000600}', json.dumps({'request_started_monotonic': 1001, 'assertion_exp': self.clock.epoch + 600}), json.dumps({'request_started_monotonic': 1000, 'assertion_exp': self.clock.epoch + 600, 'extra': 'invalid'}))
        for value in invalid:
            with self.subTest(value=value):
                self.reset()
                if value is not None:
                    self.state.write_text(value)
                def failed_fetch(*args):
                    self.requests.append(args)
                    raise OSError('synthetic-secret')
                self.assertEqual(1, self.invoke(['group', 'delete'], fetch=failed_fetch))
                self.assertEqual(1, len(self.requests))
                self.assertEqual([], self.calls)
                self.assertFalse(self.state.exists())

    def test_06_audience_bearer_and_no_redirect(self):
        self.assertEqual(0, self.invoke())
        url, headers, timeout = self.requests[0]
        self.assertEqual('issuer.invalid', parse.urlsplit(url).hostname)
        self.assertEqual([('keep', 'a+b'), ('keep', 'two'), ('audience', 'api://AzureADTokenExchange')], parse.parse_qsl(parse.urlsplit(url).query))
        self.assertIn('audience=api%3A%2F%2FAzureADTokenExchange', url)
        self.assertEqual({'Authorization': 'Bearer synthetic-bearer-secret'}, headers)
        self.assertEqual(10, timeout)
        # Exercise the production HTTP handler with a synthetic redirect response.
        with mock.patch.object(session.urllib.request, 'build_opener') as build:
            opener = build.return_value
            opener.open.side_effect = error.HTTPError(url, 302, 'redirect', {'Location': 'https://other.invalid'}, None)
            with self.assertRaises(Exception):
                session.fetch_response(url, headers, 1)
            handlers = build.call_args.args
            redirect = next(handler for handler in handlers if isinstance(handler, session.urllib.request.HTTPRedirectHandler))
            self.assertIsNone(redirect.redirect_request(None, None, 302, 'redirect', {}, 'https://other.invalid'))
            self.assertEqual(1, opener.open.call_count)

    def test_07_invalid_issuer_responses_never_forward(self):
        bad = (b'{', b'[]', b'{}', b'{"value":""}', b'{"value":42}', b'x' * 1_048_577, body('invalid'), body('a.!.c'), body(token(True)), body(token(1.5)), body(token(float('inf'))), body(token(self.clock.epoch - 1)), body(token(self.clock.epoch + 89)))
        for response in bad:
            with self.subTest(response=response[:80]):
                self.reset()
                self.assertEqual(1, self.invoke(fetch=lambda *args: response))
                self.assertEqual([], self.calls)
                self.assertFalse(self.state.exists())
        for key in ('ACTIONS_ID_TOKEN_REQUEST_URL', 'ACTIONS_ID_TOKEN_REQUEST_TOKEN'):
            env = dict(self.env)
            env.pop(key)
            self.assertEqual(1, self.invoke(environ=env))
        for url in ('http://issuer.invalid', 'https:///missing-host'):
            self.assertEqual(1, self.invoke(environ=dict(self.env, ACTIONS_ID_TOKEN_REQUEST_URL=url)))
        self.assertEqual(1, self.invoke(fetch=mock.Mock(side_effect=error.HTTPError('https://issuer.invalid', 403, 'synthetic-secret', {}, None))))
        self.assertEqual([], self.calls)

    def test_08_authentication_failure_timeout_and_secret_suppression(self):
        for stage, failure in (('fetch', TimeoutError('synthetic-secret')), ('fetch', OSError('synthetic-secret')), ('login', subprocess.TimeoutExpired('synthetic-secret', 1)), ('login', 7), ('account', subprocess.TimeoutExpired('synthetic-secret', 1)), ('account', 7)):
            with self.subTest(stage=stage, failure=type(failure).__name__):
                self.reset()
                self.stamp(age=120)
                def fetch(*args):
                    if stage == 'fetch':
                        raise failure
                    return self.fetch(*args)
                def launch(argv, timeout, quiet):
                    self.calls.append((argv, timeout, quiet))
                    self.assertTrue(quiet)
                    if argv[1] == stage:
                        if isinstance(failure, Exception):
                            raise failure
                        return failure
                    return 0
                self.assertEqual(124 if isinstance(failure, (TimeoutError, subprocess.TimeoutExpired)) else 1, self.invoke(fetch=fetch, launch=launch))
                self.assertFalse(self.state.exists())
                self.assertNotIn('synthetic-secret', self.diagnostics)
                self.assertTrue(all(call[2] for call in self.calls))
        # Real authentication output must be discarded, even if hostile.
        self.reset()
        self.configure_cli(b'synthetic-secret-stdout', b'synthetic-secret-stderr', 7)
        result = self.real(['group', 'delete'])
        self.assertEqual(1, result.returncode)
        self.assertEqual(b'', result.stdout)
        self.assertEqual(b'azure_cli_session_authentication_failed\n', result.stderr)
        self.assertEqual(['login'], [args[0] for args in self.fixture_calls()])

    def test_09_retrieval_and_login_time_count_toward_freshness(self):
        for stage in ('fetch', 'login'):
            with self.subTest(stage=stage):
                self.reset()
                assertion = token(self.clock.epoch + 95)
                def fetch(*args):
                    self.requests.append(args)
                    if stage == 'fetch':
                        self.clock.advance(6)
                    return body(assertion)
                def launch(argv, timeout, quiet):
                    self.calls.append((argv, timeout, quiet))
                    if argv[1] == 'login':
                        self.clock.advance(6)
                    return 0
                self.assertEqual(1, self.invoke(fetch=fetch, launch=launch))
                self.assertEqual('azure_cli_session_assertion_expired\n', self.diagnostics)
                self.assertEqual(1, len(self.requests))
                self.assertTrue(all(call[2] for call in self.calls))
                self.assertFalse(self.state.exists())

    def test_10_atomic_private_state_contains_no_credentials(self):
        replace = os.replace
        writes = []
        def observe(source, destination):
            writes.append((Path(source), Path(destination), stat.S_IMODE(Path(source).stat().st_mode), json.loads(Path(source).read_text())))
            self.assertFalse(self.state.exists())
            return replace(source, destination)
        with mock.patch.object(session.os, 'replace', side_effect=observe):
            self.assertEqual(0, self.invoke())
        self.assertEqual(1, len(writes))
        self.assertEqual(self.private, writes[0][0].parent)
        self.assertEqual(self.state, writes[0][1])
        self.assertEqual(0o600, writes[0][2])
        self.assertEqual({'request_started_monotonic': 1000.0, 'assertion_exp': 2_000_000_600}, writes[0][3])
        self.assertEqual(0o700, stat.S_IMODE(self.private.stat().st_mode))
        self.assertEqual({'lock', 'freshness.json'}, {p.name for p in self.private.iterdir()})
        for path in self.private.iterdir():
            self.assertEqual(0o600, stat.S_IMODE(path.stat().st_mode))
            self.assertNotIn(self.env['ACTIONS_ID_TOKEN_REQUEST_TOKEN'], path.read_text() + self.diagnostics)
            self.assertNotIn(token(self.clock.epoch + 600, '1'), path.read_text() + self.diagnostics)
        self.reset()
        with mock.patch.object(session.os, 'replace', side_effect=OSError('synthetic-secret')):
            self.assertEqual(1, self.invoke())
        self.assertFalse(self.state.exists())
        self.assertTrue(all(call[2] for call in self.calls))

    def test_11_argument_boundaries_and_no_shell(self):
        side_effect = self.root / 'should-not-exist'
        args = ['storage', 'blob', 'show', 'two words', '"quotes"', "'single'", 'line\nbreak', '$(touch ' + str(side_effect) + ')', '; touch ' + str(side_effect)]
        self.assertEqual(0, self.real(args).returncode)
        self.assertEqual(args, self.fixture_calls()[-1])
        self.assertFalse(side_effect.exists())

    def test_12_binary_streams_exits_and_nested_path_consumer(self):
        for code in (0, 7):
            with self.subTest(code=code):
                # Seed real-clock freshness so the error fixture is the requested CLI.
                self.state.write_text(json.dumps({'request_started_monotonic': time.monotonic(), 'assertion_exp': int(time.time()) + 600}))
                self.state.chmod(0o600)
                self.configure_cli(b'\x00\xffstdout\n', b'\x00\xfestderr\n', code)
                result = self.real(['resource', 'list'])
                self.assertEqual((code, b'\x00\xffstdout\n', b'\x00\xfestderr\n'), (result.returncode, result.stdout, result.stderr))
        expected = b'{"accessToken":"synthetic-access-token","expires_on":2000000000}\n'
        self.configure_cli(expected)
        env = dict(self.env, PATH=str(WRAPPER.parent) + ':' + self.env['PATH'])
        consumer = "import json,subprocess; r=subprocess.run(['az','account','get-access-token'],check=True,capture_output=True); assert json.loads(r.stdout)['accessToken']=='synthetic-access-token'; assert r.stderr==b''; print(r.stdout.hex())"
        result = subprocess.run([sys.executable, '-c', consumer], env=env, capture_output=True, timeout=5)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(expected.hex().encode() + b'\n', result.stdout)
        self.assertEqual(['account', 'get-access-token'], self.fixture_calls()[-1])

    def test_13_process_shared_lock_serializes_auth_and_forwarding(self):
        fd = self.barriers()
        first, first_result = self.start(['hold'])
        self.ready(fd)
        waiting = self.context.Event()
        second, second_result = self.start(['resource', 'list'], waiting)
        self.assertTrue(waiting.wait(3), 'second helper did not encounter held lock')
        self.assertEqual(['login', 'account', 'hold'], [args[0] for args in self.fixture_calls()])
        self.release()
        self.finish(first, first_result)
        self.finish(second, second_result)
        self.assertFalse((self.root / 'overlap').exists())
        self.assertEqual(['login', 'account', 'hold', 'resource'], [args[0] for args in self.fixture_calls()])
        self.assertEqual(['request'], (self.root / 'requests').read_text().splitlines())

    def test_14_lock_wait_and_command_share_deadline(self):
        with (self.private / 'lock').open('a') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX)
            self.assertEqual(124, self.invoke(['group', 'delete']))
            self.assertGreaterEqual(self.clock.now, 1060)
            self.assertLess(self.clock.now, 1060.051)
        self.assertEqual([], self.calls)
        self.assertEqual([], self.requests)
        self.reset()
        self.clock = Clock()
        held = (self.private / 'lock').open('a')
        fcntl.flock(held, fcntl.LOCK_EX)
        def wait(seconds):
            self.assertLessEqual(seconds, 0.05)
            self.clock.advance(40)
            held.close()
        def fetch(*args):
            self.clock.advance(5)
            return self.fetch(*args)
        def launch(argv, timeout, quiet):
            self.calls.append((argv, timeout, quiet))
            if quiet:
                self.clock.advance(5)
            else:
                raise subprocess.TimeoutExpired('synthetic', timeout)
            return 0
        self.assertEqual(124, self.invoke(sleep=wait, fetch=fetch, launch=launch))
        self.assertEqual([15, 10, 5], [call[1] for call in self.calls])
        self.assertEqual('azure_cli_session_timeout\n', self.diagnostics)

    def test_15_signals_timeout_reap_group_before_unlock(self):
        for termination in (signal.SIGINT, signal.SIGTERM, 'timeout'):
            with self.subTest(termination=termination):
                for name in ('ready', 'release', 'pids', 'heartbeat'):
                    (self.root / name).unlink(missing_ok=True)
                fd = self.barriers()
                worker, result = self.start(['tree'], budget=1 if termination == 'timeout' else None)
                self.ready(fd)
                pids = json.loads((self.root / 'pids').read_text())
                waiting = self.context.Event()
                later, later_result = self.start(['check-tree'], waiting)
                self.assertTrue(waiting.wait(3), 'later helper did not wait for tree cleanup')
                if termination != 'timeout':
                    os.kill(worker.pid, termination)
                self.finish(worker, result, 124 if termination == 'timeout' else 128 + termination)
                self.finish(later, later_result)
                with (self.private / 'lock').open('a') as lock:
                    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
                    self.assertFalse(Path('/proc', str(pids[0])).exists(), 'direct child was not reaped')
                    stat_path = Path('/proc', str(pids[1]), 'stat')
                    self.assertTrue(not stat_path.exists() or stat_path.read_text().split()[2] == 'Z', 'grandchild still running')
                    heartbeat = self.root / 'heartbeat'
                    self.assertTrue(heartbeat.is_file(), 'grandchild never wrote its heartbeat')
                    before = heartbeat.read_bytes()
                self.assertEqual(0, self.real(['resource', 'list']).returncode)
                self.assertEqual(before, heartbeat.read_bytes())

    def test_16_independent_fallback_can_refresh_after_failure(self):
        self.assertEqual(1, self.invoke(['group', 'create'], fetch=mock.Mock(side_effect=OSError('synthetic-secret'))))
        self.assertFalse(self.state.exists())
        self.assertEqual([], self.calls)
        self.assertEqual(0, self.invoke(['group', 'delete', '--name', 'synthetic']))
        self.assertEqual(['login', 'account', 'group'], [call[0][1] for call in self.calls])
        self.assertEqual([str(self.cli), 'group', 'delete', '--name', 'synthetic'], self.calls[-1][0])

    def test_17_cleanup_controls_and_acr_login_passthrough(self):
        self.stamp()
        self.assertEqual(0, self.invoke(['account', 'clear']))
        self.assertFalse(self.state.exists())
        self.assertEqual([], self.requests)
        self.assertEqual([[str(self.cli), 'account', 'clear']], [call[0] for call in self.calls])
        self.reset()
        for args in (['login', '--anything'], ['logout'], ['account', 'set', '--subscription', 'synthetic'], ['cloud', 'set', '--name', 'synthetic']):
            self.assertEqual(1, self.invoke(args))
        self.assertEqual([], self.calls)
        self.assertEqual([], self.requests)
        self.assertEqual({'azure_cli_session_control_rejected'}, set(self.diagnostics.splitlines()))
        self.reset()
        self.assertEqual(0, self.invoke(['acr', 'login', '--name', 'synthetic-registry']))
        self.assertEqual([str(self.cli), 'acr', 'login', '--name', 'synthetic-registry'], self.calls[-1][0])
        self.assertEqual(1, len([call for call in self.calls if call[0][1:3] == ['acr', 'login']]))
        self.assertEqual(0, self.invoke(['storage', 'blob', 'show', '--name', 'login']))
        self.state.write_text(json.dumps({'request_started_monotonic': time.monotonic(), 'assertion_exp': int(time.time()) + 600}))
        self.state.chmod(0o600)
        self.configure_cli(b'acr-stdout\x00', b'acr-stderr\xff', 7)
        result = self.real(['acr', 'login', '--name', 'synthetic-registry'])
        self.assertEqual((7, b'acr-stdout\x00', b'acr-stderr\xff'), (result.returncode, result.stdout, result.stderr))
        self.assertEqual([['acr', 'login', '--name', 'synthetic-registry']], self.fixture_calls())
        # Cleanup must also wait behind a running ordinary command.
        self.configure_cli()
        fd = self.barriers()
        first, first_result = self.start(['hold'])
        self.ready(fd)
        waiting = self.context.Event()
        cleanup, cleanup_result = self.start(['account', 'clear'], waiting)
        self.assertTrue(waiting.wait(3))
        self.assertNotIn(['account', 'clear'], self.fixture_calls())
        self.release()
        self.finish(first, first_result)
        self.finish(cleanup, cleanup_result)
        self.assertEqual(['account', 'clear'], self.fixture_calls()[-1])
        self.assertFalse(self.state.exists())
        self.assertFalse((self.root / 'requests').exists())
        self.configure_cli(b'cleanup-stdout', b'cleanup-stderr', 7)
        result = self.real(['account', 'clear'])
        self.assertEqual((7, b'cleanup-stdout', b'cleanup-stderr'), (result.returncode, result.stdout, result.stderr))
        self.assertFalse(self.state.exists())

    def test_18_synthetic_long_phase_renews_distinct_assertions(self):
        refreshes = []
        for elapsed in (0, 119, 120, 1500, 5400, 9600):
            self.clock.now = 1000 + elapsed
            self.clock.epoch = 2_000_000_000 + elapsed
            before = len(self.requests)
            self.assertEqual(0, self.invoke(['resource', 'list']))
            refreshes.append(len(self.requests) - before)
        self.assertEqual([1, 0, 1, 1, 1, 1], refreshes)
        assertions = [call[0][call[0].index('--federated-token') + 1] for call in self.calls if call[0][1] == 'login']
        self.assertEqual(5, len(set(assertions)))
        self.assertEqual(6, len([call for call in self.calls if not call[2]]))

    def setup_fixture(self, failure=None):
        base = self.root / ('installation-' + str(failure))
        base.mkdir()
        workspace, original = base / 'workspace', base / 'original'
        original.mkdir()
        shim = workspace / 'scripts/azure-cli-session/az'
        shim.parent.mkdir(parents=True)
        for executable, marker in ((original / 'az', 'original-calls'), (shim, 'wrapper')):
            executable.write_text('#!/bin/bash\nprintf "%s\\n" "$*" >> "' + str(base / marker) + '"\n')
            executable.chmod(0o700)
        runner = base / 'runner'
        runner.mkdir()
        env = {'PATH': str(original) + ':' + os.defpath, 'RUNNER_TEMP': str(runner), 'GITHUB_WORKSPACE': str(workspace), 'GITHUB_ENV': str(base / 'env'), 'GITHUB_PATH': str(base / 'path')}
        if failure == 'absent':
            shim.unlink()
        elif failure == 'non-executable':
            shim.chmod(0o600)
        elif failure == 'mktemp':
            (original / 'mktemp').write_text('#!/bin/bash\nexit 9\n')
            (original / 'mktemp').chmod(0o700)
        elif failure in ('env-append', 'path-append'):
            Path(env['GITHUB_ENV' if failure == 'env-append' else 'GITHUB_PATH']).mkdir()
        if failure == 'login-failed':
            result, outcome = None, 'skipped'
        else:
            result = subprocess.run(['bash', '-c', SETUP], env=env, capture_output=True, timeout=3)
            outcome = 'success' if result.returncode == 0 else 'failure'
        return base, env, result, outcome

    def later_steps(self, base, env, prepare, setup, cancelled=False, phase='failure'):
        # Interpret only the exact documented expressions, never GitHub Actions itself.
        terms = {'always()': True, '!cancelled()': not cancelled,
                 "steps.prepare.outcome == 'success'": prepare == 'success',
                 "steps.azure_session.outcome == 'success'": setup == 'success'}
        permitted = [name for name, predicate in (('export', EXPORT), ('destroy', DESTROY)) if all(terms[term] for term in predicate.split(' && '))]
        inherited = dict(env)
        env_file, path_file = Path(env['GITHUB_ENV']), Path(env['GITHUB_PATH'])
        if env_file.is_file():
            inherited.update(line.split('=', 1) for line in env_file.read_text().splitlines())
        if path_file.is_file():
            inherited['PATH'] = ':'.join(reversed(path_file.read_text().splitlines())) + ':' + inherited['PATH']
        workloads = (['image', 'readback', 'phase'] if prepare == setup == phase == 'success' else []) + permitted
        script = '\n'.join('command -v az; az ' + workload for workload in workloads)
        result = subprocess.run(['bash', '-c', script], env=inherited, capture_output=True, timeout=3)
        self.assertEqual(0, result.returncode, result.stderr)
        return permitted, result

    def test_19_literal_install_failure_blocks_later_workloads(self):
        template = (ROOT / 'docs/operations/workflows/scenario.yml.txt').read_text()
        lines = template.split('      - name: Enable renewable serialized Azure CLI session\n', 1)[1].splitlines()
        start = lines.index('        run: |') + 1
        literal = []
        for line in lines[start:]:
            if line and not line.startswith('          '):
                break
            literal.append(line[10:] if line else '')
        self.assertEqual(SETUP.rstrip(), '\n'.join(literal).rstrip())
        for failure in ('absent', 'non-executable', 'mktemp', 'env-append', 'path-append', 'login-failed'):
            with self.subTest(failure=failure):
                base, env, result, outcome = self.setup_fixture(failure)
                if result is None:
                    self.assertEqual('skipped', outcome)
                else:
                    self.assertNotEqual(0, result.returncode)
                    self.assertEqual('failure', outcome)
                permitted, later = self.later_steps(base, env, 'success', outcome, phase='success')
                self.assertEqual([], permitted)
                self.assertEqual(b'', later.stdout)
                self.assertFalse((base / 'original-calls').exists())
                self.assertFalse((base / 'wrapper').exists())
                path = Path(env['GITHUB_PATH'])
                self.assertFalse(path.is_file() and path.read_text())
                env_file = Path(env['GITHUB_ENV'])
                if failure == 'path-append':
                    self.assertEqual(2, len(env_file.read_text().splitlines()))
                elif env_file.is_file():
                    self.assertEqual('', env_file.read_text())

    def test_20_successful_setup_fallback_status_matrix(self):
        base, env, result, outcome = self.setup_fixture()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual('success', outcome)
        installed = Path(env['GITHUB_WORKSPACE']) / 'scripts/azure-cli-session/az'
        self.assertEqual(str(installed.parent) + '\n', Path(env['GITHUB_PATH']).read_text())
        values = dict(line.split('=', 1) for line in Path(env['GITHUB_ENV']).read_text().splitlines())
        self.assertEqual(str(base / 'original/az'), values['PARCHWELL_REAL_AZ'])
        self.assertEqual(0o700, stat.S_IMODE(Path(values['PARCHWELL_AZ_SESSION_DIRECTORY']).stat().st_mode))
        cases = [('success', 'success', False, ['export', 'destroy']), ('success', 'success', True, ['destroy']), ('failure', 'success', False, []), ('skipped', 'success', False, []), ('success', 'failure', False, []), ('success', 'cancelled', False, []), ('success', 'skipped', False, [])]
        for prepare, setup, cancelled, expected in cases:
            with self.subTest(prepare=prepare, setup=setup, cancelled=cancelled):
                (base / 'wrapper').unlink(missing_ok=True)
                permitted, later = self.later_steps(base, env, prepare, setup, cancelled)
                self.assertEqual(expected, permitted)
                self.assertEqual([str(installed)] * len(expected), later.stdout.decode().splitlines())
                marker = base / 'wrapper'
                self.assertEqual(expected, marker.read_text().splitlines() if marker.exists() else [])
                self.assertFalse((base / 'original-calls').exists())


    def test_21_cleanup_waits_for_group_observation_before_unlock(self):
        self.state.write_text(json.dumps({'request_started_monotonic': time.monotonic(),
                                         'assertion_exp': int(time.time()) + 600}))
        self.state.chmod(0o600)
        process = mock.Mock(pid=4242)
        process.wait.side_effect = [session.SessionInterrupted(signal.SIGINT), 0, 0]
        events = []

        def assert_locked():
            with (self.private / 'lock').open('a') as independent:
                with self.assertRaises(BlockingIOError):
                    fcntl.flock(independent, fcntl.LOCK_EX | fcntl.LOCK_NB)

        observations = iter([PermissionError, ValueError, ['R'], ['X'], ['Z']])

        def observe(pgid):
            self.assertEqual(4242, pgid)
            value = next(observations)
            events.append(('observe', value if isinstance(value, type) else value[0]))
            assert_locked()
            for number in (signal.SIGINT, signal.SIGTERM, signal.SIGALRM):
                self.assertEqual(signal.SIG_IGN, signal.getsignal(number))
            if isinstance(value, type):
                raise value()
            return value

        def poll(seconds):
            self.assertEqual(0.05, seconds)
            assert_locked()
            events.append(('sleep', seconds))

        stderr = io.StringIO()
        with mock.patch.object(session.subprocess, 'Popen', return_value=process), \
                mock.patch.object(session.os, 'killpg', side_effect=lambda pgid, number: events.append(('signal', pgid, number))), \
                mock.patch.object(session, 'process_group_states', side_effect=observe, create=True) as observed, \
                mock.patch.object(session.time, 'sleep', side_effect=poll), \
                contextlib.redirect_stderr(stderr):
            self.assertEqual(130, session.main(['resource', 'list'], environ=self.env))
        with (self.private / 'lock').open('a') as independent:
            fcntl.flock(independent, fcntl.LOCK_EX | fcntl.LOCK_NB)
            events.append(('unlock',))
        self.assertEqual([
            ('signal', 4242, signal.SIGTERM), ('signal', 4242, signal.SIGKILL),
            ('observe', PermissionError), ('sleep', 0.05),
            ('observe', ValueError), ('sleep', 0.05),
            ('observe', 'R'), ('sleep', 0.05),
            ('observe', 'X'), ('sleep', 0.05),
            ('observe', 'Z'), ('unlock',)], events)
        self.assertEqual(3, process.wait.call_count)
        self.assertEqual(5, observed.call_count)
        with self.assertRaises(StopIteration):
            next(observations)
        self.assertEqual('azure_cli_session_cleanup_observation_failed\n', stderr.getvalue())

        cases = [
            ('empty', None, None, []),
            ('running', '501', '501 (comm) R 1 4242 0', ['R']),
            ('dead', '501', '501 (comm) X 1 4242 0', ['X']),
            ('zombie', '501', '501 (comm) Z 1 4242 0', ['Z']),
            ('other group', '501', '501 (comm) R 1 9999 0', []),
            ('nonnumeric', 'self', None, []),
            ('parenthesized comm', '501', '501 (odd ) name) R 1 4242 0', ['R']),
            ('vanished file', '501', FileNotFoundError, []),
            ('vanished process', '501', ProcessLookupError, []),
            ('permission', '501', PermissionError, PermissionError),
            ('missing close', '501', '501 (comm', IndexError),
            ('missing tail', '501', '501 (comm)', IndexError),
            ('noninteger group', '501', '501 (comm) R 1 bad 0', ValueError),
        ]
        for label, name, raw, expected in cases:
            with self.subTest(parser=label):
                entry = mock.MagicMock()
                entry.name = name
                stat_file = entry.__truediv__.return_value
                if isinstance(raw, type):
                    stat_file.read_text.side_effect = raw
                else:
                    stat_file.read_text.return_value = raw
                with mock.patch.object(session, 'Path') as proc_path:
                    proc_path.return_value.iterdir.return_value = [] if name is None else [entry]
                    if isinstance(expected, type):
                        with self.assertRaises(expected):
                            session.process_group_states(4242)
                    else:
                        self.assertEqual(expected, session.process_group_states(4242))
                    proc_path.assert_called_once_with('/proc')
                    if name == 'self':
                        stat_file.read_text.assert_not_called()


if __name__ == '__main__':
    unittest.main()

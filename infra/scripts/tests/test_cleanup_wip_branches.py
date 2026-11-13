"""Synthetic offline Git repositories exercise the main-only cleanup contract."""

from copy import deepcopy
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
from urllib import error

import yaml


ROOT = Path(__file__).resolve().parents[3]
SCRIPT = ROOT / 'scripts/cleanup-wip-branches.py'
SPEC = importlib.util.spec_from_file_location('cleanup_wip_branches', SCRIPT)
CLEANUP = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = CLEANUP
SPEC.loader.exec_module(CLEANUP)
TOKEN = 'synthetic-workflow-credential-marker'
BACKUP = 'refs/heads/codex/example-wip'
MATCHING = 'refs/heads/codex/example'
MAIN = CLEANUP.MAIN
RULES = [{'type': kind, 'ruleset_id': CLEANUP.RULESET_ID,
          'ruleset_source_type': 'Repository', 'ruleset_source': CLEANUP.REPOSITORY}
         for kind in ('deletion', 'non_fast_forward')]
RULESET = {
    'id': CLEANUP.RULESET_ID, 'target': 'branch', 'enforcement': 'active',
    'conditions': {'ref_name': {'include': ['~DEFAULT_BRANCH'], 'exclude': []}},
    'rules': [{'type': 'deletion'}, {'type': 'non_fast_forward'}],
    'bypass_actors': [{'actor_type': 'OrganizationAdmin', 'bypass_mode': 'pull_request'},
                      {'actor_type': 'RepositoryRole', 'actor_id': 5, 'bypass_mode': 'pull_request'}],
}


def git(directory, *arguments, check=True, input=None):
    return subprocess.run(['git', *arguments], cwd=directory, check=check, input=input,
                          capture_output=True, text=True, timeout=30)


class Clock:
    def __init__(self):
        self.now = 0

    def __call__(self):
        return self.now


class Response:
    def __init__(self, url, data, status=200):
        self.url, self.data, self.status = url, data, status

    def __enter__(self):
        return self

    def __exit__(self, *arguments):
        return False

    def geturl(self):
        return self.url

    def read(self, size):
        return self.data[:size]


class Opener:
    """Only the protection API boundary is stubbed for ordinary Git tests."""
    def __init__(self, rules=None, ruleset=None, respond=None):
        self.rules = deepcopy(RULES if rules is None else rules)
        self.ruleset = deepcopy(RULESET if ruleset is None else ruleset)
        self.respond = respond
        self.calls = []

    def open(self, req, timeout):
        self.calls.append((req, timeout))
        if self.respond is not None:
            return self.respond(req, timeout, len(self.calls) - 1)
        value = self.rules if req.full_url == CLEANUP.RULES_URL else self.ruleset
        return Response(req.full_url, json.dumps(value).encode())


class Repository:
    """A temporary bare origin contains actual distinct synthetic Git commits."""
    def __init__(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='cleanup-wip-test-')
        self.root = Path(self.temporary.name)
        self.remote, self.work = self.root / 'origin.git', self.root / 'work'
        self.work.mkdir()
        git(self.root, 'init', '--bare', str(self.remote))
        git(self.work, 'init')
        git(self.work, 'config', 'user.name', 'Synthetic fixture')
        git(self.work, 'config', 'user.email', 'synthetic@example.invalid')
        git(self.work, 'remote', 'add', 'origin', str(self.remote))
        tree = git(self.work, 'mktree', input='').stdout.strip()
        def commit(label, parent=None):
            arguments = ['commit-tree', tree, '-m', f'synthetic {label}']
            if parent is not None:
                arguments += ['-p', parent]
            return git(self.work, *arguments).stdout.strip()
        self.a = commit('A')
        self.b = commit('B', self.a)
        self.c = commit('C', self.a)
        self.d = commit('D', self.b)
        self.unrelated = commit('unrelated root')
        git(self.work, 'push', 'origin', *[
            f'{head}:refs/synthetic/{label}' for label, head in
            (('A', self.a), ('B', self.b), ('C', self.c), ('D', self.d), ('unrelated', self.unrelated))])
        self.set(MAIN, self.b)

    def close(self):
        self.temporary.cleanup()

    def set(self, ref, head):
        if head is None:
            git(self.remote, 'update-ref', '-d', ref)
        else:
            git(self.remote, 'update-ref', ref, head)

    def ref(self, ref):
        result = git(self.remote, 'rev-parse', '--verify', '--quiet', ref, check=False)
        return result.stdout.strip() if result.returncode == 0 else None

    def protect_main(self):
        hook = self.remote / 'hooks/update'
        hook.write_text('''#!/bin/sh
if [ "$1" = refs/heads/main ]; then
  if [ "$3" = 0000000000000000000000000000000000000000 ]; then exit 1; fi
  if [ "$2" != 0000000000000000000000000000000000000000 ]; then
    git merge-base --is-ancestor "$2" "$3" || exit 1
  fi
fi
''')
        hook.chmod(0o700)


class CleanupWipBranchesTests(unittest.TestCase):
    def setUp(self):
        self.repository = Repository()
        self.addCleanup(self.repository.close)

    def run_cleanup(self, runner=None, opener=None, clock=None, protection=None):
        clock = clock if clock is not None else Clock()
        deadline = CLEANUP.Deadline(clock)
        client = CLEANUP.Git(self.repository.work, deadline,
                             subprocess.run if runner is None else runner)
        guard = protection if protection is not None else CLEANUP.Protection(
            TOKEN, deadline, Opener() if opener is None else opener)
        return CLEANUP.cleanup(client, guard, deadline)

    def rows(self, report):
        return {row.branch: row for row in report.rows}

    def recording_runner(self, before=None, fail=None):
        calls = []
        def runner(arguments, **kwargs):
            calls.append(arguments)
            if before is not None:
                before(arguments)
            if fail is not None:
                result = fail(arguments)
                if result is not None:
                    return result
            return subprocess.run(arguments, **kwargs)
        return runner, calls

    def assert_no_push(self, calls):
        self.assertFalse(any('push' in call for call in calls), calls)

    def test_production_context_fails_closed(self):
        valid = {'GITHUB_REPOSITORY': CLEANUP.REPOSITORY, 'GITHUB_REF': MAIN, 'GITHUB_TOKEN': TOKEN}
        contexts = [{}, dict(valid, GITHUB_REPOSITORY='unrelated/repository'),
                    {key: value for key, value in valid.items() if key != 'GITHUB_REF'},
                    dict(valid, GITHUB_REF='refs/tags/main'), dict(valid, GITHUB_REF='refs/heads/feature')]
        canonical = f'https://github.com/{CLEANUP.REPOSITORY}'
        invalid_origins = ['/tmp/local.git', 'https://github.com/unrelated/repository.git',
                           f'https://{TOKEN}@github.com/{CLEANUP.REPOSITORY}.git']
        for context, origin, push_origin, expected in [
                *[(context, canonical, canonical, 1) for context in contexts],
                *[(valid, origin, origin, 1) for origin in invalid_origins],
                (valid, canonical, invalid_origins[1], 1),
                (valid, canonical, canonical, 0), (valid, canonical + '.git', canonical + '.git', 0)]:
            with self.subTest(context=context, origin=origin, push_origin=push_origin):
                calls = []
                def fake_git(arguments, **kwargs):
                    calls.append(arguments)
                    url = push_origin if '--push' in arguments else origin
                    return subprocess.CompletedProcess(arguments, 0, url + '\n', '')
                original_git = CLEANUP.Git
                with patch.object(CLEANUP, 'Git', side_effect=lambda directory, deadline: original_git(directory, deadline, fake_git)), \
                        patch.object(CLEANUP, 'cleanup', return_value=CLEANUP.Report()) as cleanup_call, \
                        patch.object(CLEANUP.shutil, 'which', return_value='/synthetic/git'), \
                        patch.dict(os.environ, context, clear=True), patch.object(sys, 'argv', [str(SCRIPT)]), \
                        patch('sys.stdout', new_callable=io.StringIO) as output:
                    self.assertEqual(CLEANUP.main(), expected)
                    self.assertEqual(cleanup_call.call_count, int(expected == 0))
                    self.assert_no_push(calls)
                    self.assertNotIn(TOKEN, output.getvalue())
        deadline = CLEANUP.Deadline(Clock())
        with patch.object(CLEANUP.shutil, 'which', return_value=None), self.assertRaises(CLEANUP.CleanupError):
            CLEANUP.validate_context(CLEANUP.Git(self.repository.work, deadline), valid)
        with patch.object(sys, 'argv', [str(SCRIPT), '--local-remote-override']), \
                patch('sys.stdout', new_callable=io.StringIO), patch.dict(os.environ, valid, clear=True):
            self.assertEqual(CLEANUP.main(), 1)

    def test_only_final_wip_heads_are_candidates(self):
        refs = ['refs/heads/codex/a-wip', 'refs/heads/codex/a-wip-extra',
                'refs/heads/codex/wip', 'refs/tags/tag-wip']
        for ref in refs:
            self.repository.set(ref, self.repository.a)
        report = self.run_cleanup()
        self.assertEqual([row.branch for row in report.rows], [refs[0]])
        self.assertEqual(report.exit_code, 0)
        self.assertIsNone(self.repository.ref(refs[0]))
        for ref in refs[1:]:
            self.assertEqual(self.repository.ref(ref), self.repository.a)

    def test_matching_branch_alone_cannot_authorize_deletion(self):
        for matching in (self.repository.b, self.repository.a):
            with self.subTest(matching=matching):
                self.repository.set(BACKUP, self.repository.a)
                self.repository.set(MATCHING, matching)
                self.repository.set(MAIN, self.repository.unrelated)
                before = {ref: self.repository.ref(ref) for ref in (BACKUP, MATCHING, MAIN)}
                report = self.run_cleanup()
                self.assertEqual(report.exit_code, 0)
                self.assertEqual(self.rows(report)[BACKUP].status, 'retained_unmerged')
                self.assertEqual(self.rows(report)[BACKUP].head, self.repository.a)
                self.assertEqual({ref: self.repository.ref(ref) for ref in before}, before)
                self.assertEqual(sum(row.status == 'deleted' for row in report.rows), 0)

    def test_equal_main_head_is_deleted(self):
        for matching in (self.repository.c, None):
            with self.subTest(matching=matching):
                self.repository.set(BACKUP, self.repository.b)
                self.repository.set(MATCHING, matching)
                report = self.run_cleanup()
                self.assertEqual(report.exit_code, 0)
                self.assertEqual(self.rows(report)[BACKUP].status, 'deleted')
                self.assertEqual(self.rows(report)[BACKUP].head, self.repository.b)
                self.assertIsNone(self.repository.ref(BACKUP))
                self.assertEqual(self.repository.ref(MAIN), self.repository.b)
                self.assertEqual(self.repository.ref(MATCHING), matching)

    def test_merged_backup_without_matching_branch_is_deleted(self):
        self.repository.set(BACKUP, self.repository.a)
        report = self.run_cleanup()
        self.assertEqual(report.exit_code, 0)
        row = self.rows(report)[BACKUP]
        self.assertEqual((row.status, row.head, row.preserved_by),
                         ('deleted', self.repository.a, f'{MAIN} at {self.repository.b}'))
        self.assertIsNone(self.repository.ref(BACKUP))
        self.assertIsNone(self.repository.ref(MATCHING))

    def test_unmerged_backup_is_retained(self):
        for matching in (self.repository.c, None):
            with self.subTest(matching=matching):
                self.repository.set(MAIN, self.repository.a)
                self.repository.set(BACKUP, self.repository.b)
                self.repository.set(MATCHING, matching)
                report = self.run_cleanup()
                self.assertEqual(report.exit_code, 0)
                self.assertEqual(self.rows(report)[BACKUP].status, 'retained_unmerged')
                self.assertEqual(self.repository.ref(BACKUP), self.repository.b)
                self.assertEqual(sum(row.status == 'deleted' for row in report.rows), 0)

    def test_missing_main_or_history_is_error(self):
        self.repository.set(BACKUP, self.repository.a)
        for operation in ('missing-main', 'fetch', 'shallow', 'missing-commit', 'ancestry'):
            with self.subTest(operation=operation):
                self.repository.set(MAIN, None if operation == 'missing-main' else self.repository.b)
                def fail(arguments):
                    if operation == 'fetch' and 'fetch' in arguments:
                        return subprocess.CompletedProcess(arguments, 128, '', TOKEN)
                    if operation == 'shallow' and '--is-shallow-repository' in arguments:
                        return subprocess.CompletedProcess(arguments, 0, 'true\n', '')
                    if operation == 'missing-commit' and 'rev-list' in arguments:
                        return subprocess.CompletedProcess(arguments, 128, '', TOKEN)
                    if operation == 'ancestry' and 'merge-base' in arguments:
                        return subprocess.CompletedProcess(arguments, 128, '', TOKEN)
                    return None
                runner, calls = self.recording_runner(fail=fail)
                report = self.run_cleanup(runner=runner)
                self.assertEqual(report.exit_code, 1)
                self.assertEqual(self.rows(report)[BACKUP].status, 'retained_error')
                self.assertEqual(self.repository.ref(BACKUP), self.repository.a)
                self.assert_no_push(calls)
                self.assertNotIn(TOKEN, report.render())
        self.repository.set(MAIN, self.repository.c)
        self.repository.set(BACKUP, self.repository.b)
        report = self.run_cleanup()
        self.assertEqual(report.exit_code, 0)
        self.assertEqual(self.rows(report)[BACKUP].status, 'retained_unmerged')

    def test_backup_changed_before_delete_is_retained(self):
        self.repository.set(BACKUP, self.repository.a)
        changed = False
        def before(arguments):
            nonlocal changed
            if 'ls-remote' in arguments and BACKUP in arguments and not changed:
                self.repository.set(BACKUP, self.repository.c)
                changed = True
        runner, calls = self.recording_runner(before=before)
        report = self.run_cleanup(runner=runner)
        self.assertEqual(report.exit_code, 0)
        self.assertEqual(self.rows(report)[BACKUP].status, 'retained_changed')
        self.assertEqual(self.repository.ref(BACKUP), self.repository.c)
        self.assert_no_push(calls)

    def test_main_changed_before_reread_is_retained(self):
        self.repository.set(BACKUP, self.repository.a)
        changed = False
        def before(arguments):
            nonlocal changed
            if 'ls-remote' in arguments and BACKUP in arguments and not changed:
                self.repository.set(MAIN, self.repository.d)
                changed = True
        runner, calls = self.recording_runner(before=before)
        report = self.run_cleanup(runner=runner)
        self.assertEqual(report.exit_code, 0)
        self.assertEqual(self.rows(report)[BACKUP].status, 'retained_changed')
        self.assertEqual(self.repository.ref(BACKUP), self.repository.a)
        self.assert_no_push(calls)

    def test_matching_branch_race_cannot_remove_preserving_copy(self):
        for new_matching in (self.repository.c, None):
            with self.subTest(new_matching=new_matching):
                self.repository.set(BACKUP, self.repository.a)
                self.repository.set(MATCHING, self.repository.b)
                self.repository.set(MAIN, self.repository.b)
                def before(arguments):
                    if 'push' in arguments:
                        self.repository.set(MATCHING, new_matching)
                runner, _ = self.recording_runner(before=before)
                report = self.run_cleanup(runner=runner)
                self.assertEqual(report.exit_code, 0)
                self.assertEqual(self.rows(report)[BACKUP].status, 'deleted')
                self.assertIsNone(self.repository.ref(BACKUP))
                self.assertEqual(self.repository.ref(MAIN), self.repository.b)
                git(self.repository.remote, 'merge-base', '--is-ancestor', self.repository.a, MAIN)
        # A disjoint root makes the matching branch the only preserving copy.
        self.repository.set(MAIN, self.repository.unrelated)
        self.repository.set(BACKUP, self.repository.a)
        self.repository.set(MATCHING, self.repository.b)
        runner, calls = self.recording_runner()
        report = self.run_cleanup(runner=runner)
        self.assertEqual(self.rows(report)[BACKUP].status, 'retained_unmerged')
        self.assertEqual(self.repository.ref(BACKUP), self.repository.a)
        self.assert_no_push(calls)

    def test_main_advance_after_reread_keeps_commit_reachable(self):
        self.repository.protect_main()
        self.repository.set(BACKUP, self.repository.a)
        def before(arguments):
            if 'push' in arguments:
                git(self.repository.work, 'push', 'origin', f'{self.repository.d}:{MAIN}')
        runner, _ = self.recording_runner(before=before)
        report = self.run_cleanup(runner=runner)
        self.assertEqual(report.exit_code, 0)
        self.assertEqual(self.rows(report)[BACKUP].status, 'deleted')
        self.assertIsNone(self.repository.ref(BACKUP))
        self.assertEqual(self.repository.ref(MAIN), self.repository.d)
        git(self.repository.remote, 'merge-base', '--is-ancestor', self.repository.a, MAIN)
        for refspec in (f':{MAIN}', f'+{self.repository.c}:{MAIN}'):
            self.assertNotEqual(git(self.repository.work, 'push', 'origin', refspec, check=False).returncode, 0)
            self.assertEqual(self.repository.ref(MAIN), self.repository.d)

    def test_exact_lease_preserves_racing_backup(self):
        self.repository.set(BACKUP, self.repository.a)
        def before(arguments):
            if 'push' in arguments:
                self.repository.set(BACKUP, self.repository.c)
        runner, calls = self.recording_runner(before=before)
        report = self.run_cleanup(runner=runner)
        self.assertEqual(report.exit_code, 0)
        self.assertEqual(self.rows(report)[BACKUP].status, 'retained_changed')
        self.assertEqual(self.repository.ref(BACKUP), self.repository.c)
        pushes = [arguments for arguments in calls if 'push' in arguments]
        self.assertEqual(len(pushes), 1)
        self.assertIn(f'--force-with-lease={BACKUP}:{self.repository.a}', pushes[0])
        self.assertEqual(pushes[0][-2:], ['origin', f':{BACKUP}'])

    def test_permission_transport_and_timeout_errors_are_visible(self):
        first, second = 'refs/heads/codex/a-wip', 'refs/heads/codex/b-wip'
        for mode in ('refusal', 'listing', 'timeout'):
            with self.subTest(mode=mode):
                self.repository.set(first, self.repository.a)
                self.repository.set(second, self.repository.a)
                failed = False
                def fail(arguments):
                    nonlocal failed
                    if mode == 'listing' and 'ls-remote' in arguments:
                        return subprocess.CompletedProcess(arguments, 128, '', TOKEN)
                    if mode == 'refusal' and 'push' in arguments and arguments[-1] == f':{first}':
                        return subprocess.CompletedProcess(arguments, 128, '', TOKEN)
                    if mode == 'timeout' and 'fetch' in arguments and not failed:
                        failed = True
                        raise subprocess.TimeoutExpired(arguments, 60, output=TOKEN)
                    return None
                runner, calls = self.recording_runner(fail=fail)
                report = self.run_cleanup(runner=runner)
                self.assertEqual(report.exit_code, 1)
                self.assertNotIn(TOKEN, report.render())
                if mode == 'listing':
                    self.assertEqual(report.rows, [])
                    self.assertIn('listing remote heads', report.errors[0])
                    self.assert_no_push(calls)
                else:
                    self.assertEqual(self.rows(report)[first].status, 'retained_error')
                    self.assertEqual(self.rows(report)[second].status, 'deleted')
                    self.assertEqual(self.repository.ref(first), self.repository.a)
                    self.assertIsNone(self.repository.ref(second))
                    self.assertEqual(len([call for call in calls if 'push' in call and call[-1] == f':{first}']),
                                     int(mode == 'refusal'))
                    self.assertIn('timed out' if mode == 'timeout' else 'deleting backup', self.rows(report)[first].reason)

    def test_branch_processing_boundaries(self):
        for count, processed, deferred in ((0, 0, 0), (99, 99, 0), (100, 100, 0), (101, 100, 1)):
            with self.subTest(count=count):
                refs = [f'refs/heads/codex/bound-{index:03}-wip' for index in range(count)]
                for ref in refs:
                    self.repository.set(ref, self.repository.a)
                runner, calls = self.recording_runner()
                report = self.run_cleanup(runner=runner)
                self.assertEqual(report.exit_code, 0)
                self.assertEqual(len(report.rows), count)
                self.assertEqual(len([call for call in calls if 'push' in call]), processed)
                self.assertEqual(sum(row.status == 'deleted' for row in report.rows), processed)
                self.assertEqual(sum(row.status == 'deferred_limit' for row in report.rows), deferred)
                if deferred:
                    self.assertEqual(self.rows(report)[refs[-1]].reason, '100-branch processing limit reached')
                for ref in refs:
                    self.repository.set(ref, None)
        for elapsed in (899, 900, 901):
            with self.subTest(elapsed=elapsed):
                self.repository.set(BACKUP, self.repository.a)
                clock = Clock()
                class Guard:
                    def check(self):
                        clock.now = elapsed
                runner, calls = self.recording_runner()
                report = self.run_cleanup(runner=runner, clock=clock, protection=Guard())
                self.assertEqual(report.exit_code, int(elapsed >= 900))
                self.assertEqual(self.rows(report)[BACKUP].status, 'retained_error' if elapsed >= 900 else 'deleted')
                if elapsed >= 900:
                    self.assert_no_push(calls)
                    self.assertEqual(self.repository.ref(BACKUP), self.repository.a)

    def test_report_is_complete_and_escaped(self):
        ref = f"refs/heads/escaped&<'`{TOKEN}-wip"
        git(self.repository.work, 'check-ref-format', ref)
        report = CLEANUP.Report(rows=[CLEANUP.Row(ref, self.repository.a, status,
                     f'{MAIN} at {self.repository.b}', 'synthetic reason') for status in CLEANUP.STATUSES])
        rendered = report.render((TOKEN,))
        self.assertNotIn(TOKEN, rendered)
        self.assertIn('&amp;', rendered)
        self.assertIn('&lt;', rendered)
        self.assertIn('&#96;', rendered)
        self.assertIn("'", rendered)
        self.assertNotIn('&amp;#', rendered)
        self.assertNotIn('&#35;x27;', rendered)
        self.assertIn(self.repository.b, rendered)
        self.assertIn('Enumerated total: 5', rendered)
        for status in CLEANUP.STATUSES:
            self.assertIn(f'{status}: 1\n', rendered)
        self.assertEqual(len([line for line in rendered.splitlines() if line.startswith('| refs')]), 5)
        empty = self.run_cleanup()
        self.assertEqual(empty.exit_code, 0)
        self.assertIn('Enumerated total: 0', empty.render())
        with tempfile.TemporaryDirectory() as directory:
            summary = Path(directory) / 'summary.md'
            environment = {'GITHUB_STEP_SUMMARY': str(summary), 'GITHUB_TOKEN': TOKEN}
            with patch.object(CLEANUP, 'validate_context'), patch.object(CLEANUP, 'cleanup', return_value=report), \
                    patch.dict(os.environ, environment, clear=True), patch.object(sys, 'argv', [str(SCRIPT)]), \
                    patch('sys.stdout', new_callable=io.StringIO) as output:
                self.assertEqual(CLEANUP.main(), 1)
                self.assertEqual(summary.read_text(), output.getvalue())
                self.assertNotIn(TOKEN, summary.read_text())

    def test_main_protection_is_required_and_refreshed(self):
        for actors in ([], RULESET['bypass_actors']):
            ruleset = deepcopy(RULESET)
            ruleset['bypass_actors'] = actors
            self.repository.set(BACKUP, self.repository.a)
            opener = Opener(ruleset=ruleset)
            report = self.run_cleanup(opener=opener)
            self.assertEqual(report.exit_code, 0)
            self.assertEqual(self.rows(report)[BACKUP].status, 'deleted')
            self.assertEqual([req.full_url for req, _ in opener.calls],
                             [CLEANUP.RULES_URL, CLEANUP.RULESET_URL] * 2)
            for req, timeout in opener.calls:
                self.assertEqual(req.get_method(), 'GET')
                self.assertEqual(req.get_header('Accept'), 'application/vnd.github+json')
                self.assertEqual(req.get_header('X-github-api-version'), '2026-03-10')
                self.assertEqual(req.get_header('Authorization'), f'Bearer {TOKEN}')
                self.assertEqual(timeout, 10)
        variants = []
        for kind in ('deletion', 'non_fast_forward'):
            variants.append(([rule for rule in RULES if rule['type'] != kind], deepcopy(RULESET)))
            ruleset = deepcopy(RULESET)
            ruleset['rules'] = [rule for rule in ruleset['rules'] if rule['type'] != kind]
            variants.append((deepcopy(RULES), ruleset))
        for field, value in (('ruleset_source_type', 'Organization'), ('ruleset_source', 'unrelated/repository'),
                             ('ruleset_id', 1)):
            rules = deepcopy(RULES)
            rules[0][field] = value
            variants.append((rules, deepcopy(RULESET)))
        for field, value in (('id', 1), ('target', 'tag'), ('enforcement', 'evaluate'),
                             ('conditions', {'ref_name': {'include': ['refs/heads/main'], 'exclude': []}}),
                             ('bypass_actors', None)):
            ruleset = deepcopy(RULESET)
            ruleset[field] = value
            variants.append((deepcopy(RULES), ruleset))
        missing = deepcopy(RULESET)
        del missing['bypass_actors']
        variants.append((deepcopy(RULES), missing))
        for mode in ('always', 'exempt', None, 'unknown'):
            ruleset = deepcopy(RULESET)
            ruleset['bypass_actors'] = [{} if mode is None else {'bypass_mode': mode}]
            variants.append((deepcopy(RULES), ruleset))
        for rules, ruleset in variants:
            with self.subTest(rules=rules, ruleset=ruleset):
                self.repository.set(BACKUP, self.repository.a)
                runner, calls = self.recording_runner()
                report = self.run_cleanup(runner=runner, opener=Opener(rules, ruleset))
                self.assertEqual(report.exit_code, 1)
                self.assertEqual(self.rows(report)[BACKUP].status, 'retained_error')
                self.assertEqual(self.rows(report)[BACKUP].reason, 'main protection could not be established')
                self.assertEqual(self.repository.ref(BACKUP), self.repository.a)
                self.assert_no_push(calls)
        for successful_deletions in (0, 1):
            with self.subTest(successful_deletions=successful_deletions):
                refs = [f'refs/heads/codex/guard-{index}-wip' for index in range(3)]
                self.repository.set(BACKUP, None)
                for ref in refs:
                    self.repository.set(ref, self.repository.a)
                def respond(req, timeout, index):
                    valid = index < 2 + 2 * successful_deletions
                    value = RULES if req.full_url == CLEANUP.RULES_URL else RULESET
                    if not valid and req.full_url == CLEANUP.RULES_URL:
                        value = []
                    return Response(req.full_url, json.dumps(value).encode())
                report = self.run_cleanup(opener=Opener(respond=respond))
                self.assertEqual(report.exit_code, 1)
                self.assertEqual([row.status for row in report.rows],
                                 ['deleted'] * successful_deletions + ['retained_error'] * (3 - successful_deletions))
                for ref in refs[successful_deletions:]:
                    self.assertEqual(self.rows(report)[ref].reason, 'main protection could not be established')
                    self.assertEqual(self.repository.ref(ref), self.repository.a)
                for ref in refs:
                    self.repository.set(ref, None)

    def test_main_protection_api_errors_fail_closed(self):
        def http_error(code):
            def respond(req, timeout, index):
                raise error.HTTPError(req.full_url, code, TOKEN, {}, None)
            return respond
        def response_bytes(data, status=200, redirected=False):
            return lambda req, timeout, index: Response(
                'https://unrelated.invalid' if redirected else req.full_url, data, status)
        def timed_out(req, timeout, index):
            raise TimeoutError(TOKEN)
        missing = deepcopy(RULES)
        del missing[0]['ruleset_source_type']
        error_responders = [http_error(code) for code in (403, 404, 500, 302)] + [
            response_bytes(b'not JSON'), response_bytes(b'{}'), response_bytes(b'[null]'),
            response_bytes(json.dumps(missing).encode()), response_bytes(b'[]', redirected=True),
            timed_out, response_bytes(b' ' * (CLEANUP.HTTP_BYTES + 1))]
        for respond in error_responders:
            with self.subTest(respond=respond):
                self.repository.set(BACKUP, self.repository.a)
                runner, calls = self.recording_runner()
                opener = Opener(respond=respond)
                report = self.run_cleanup(runner=runner, opener=opener)
                self.assertEqual(report.exit_code, 1)
                self.assertEqual(self.rows(report)[BACKUP].status, 'retained_error')
                self.assertEqual(self.rows(report)[BACKUP].reason, 'main protection could not be established')
                self.assertEqual(self.repository.ref(BACKUP), self.repository.a)
                self.assertNotIn(TOKEN, report.render())
                self.assertLessEqual(len(opener.calls), 2)
                self.assert_no_push(calls)
        def padded(req, timeout, index):
            value = RULES if req.full_url == CLEANUP.RULES_URL else RULESET
            data = json.dumps(value).encode()
            return Response(req.full_url, data + b' ' * (CLEANUP.HTTP_BYTES - len(data)))
        self.repository.set(BACKUP, self.repository.a)
        report = self.run_cleanup(opener=Opener(respond=padded))
        self.assertEqual(report.exit_code, 0)
        self.assertEqual(self.rows(report)[BACKUP].status, 'deleted')
        report = self.run_cleanup(opener=Opener(respond=http_error(403)))
        self.assertEqual(report.rows, [])
        self.assertEqual(report.exit_code, 1)
        self.assertEqual(report.errors, ['main protection could not be established'])
        self.assertTrue(any(isinstance(handler, CLEANUP.NoRedirect)
                            for handler in CLEANUP.Protection(TOKEN, CLEANUP.Deadline(Clock())).opener.handlers))

    def test_workflow_contract_and_mirror(self):
        workflow = ROOT / '.github/workflows/cleanup-wip-branches.yml'
        self.assertEqual(workflow.read_bytes(), (ROOT / 'docs/operations/workflows/cleanup-wip-branches.yml.txt').read_bytes())
        document = yaml.load(workflow.read_text(), Loader=yaml.BaseLoader)
        self.assertEqual(document['name'], 'Cleanup backup branches')
        self.assertEqual(document['on'], {'workflow_dispatch': ''})
        self.assertEqual(document['permissions'], {'contents': 'write'})
        self.assertEqual(document['concurrency'], {'group': 'cleanup-wip-branches', 'cancel-in-progress': 'false'})
        self.assertEqual(len(document['jobs']), 1)
        job = next(iter(document['jobs'].values()))
        self.assertEqual(job['if'], "${{ github.repository == 'collaborationwithothers/eventhubs-kafka-dotnet' && github.ref == 'refs/heads/main' }}")
        self.assertEqual(job['runs-on'], 'ubuntu-24.04')
        self.assertEqual(job['timeout-minutes'], '20')
        self.assertNotIn('permissions', job)
        checkout, execute = job['steps']
        self.assertEqual(checkout['uses'], 'actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1')
        self.assertEqual(checkout['with'], {'ref': '${{ github.sha }}', 'fetch-depth': '0', 'persist-credentials': 'true'})
        self.assertEqual(execute['run'], 'python3 scripts/cleanup-wip-branches.py')
        self.assertEqual(execute['env'], {'GITHUB_TOKEN': '${{ github.token }}'})


if __name__ == '__main__':
    unittest.main()

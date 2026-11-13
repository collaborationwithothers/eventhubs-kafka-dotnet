#!/usr/bin/env python3
"""Clean redundant backups under the approved issue 448 main-only contract."""

from collections import Counter
from contextlib import contextmanager
from dataclasses import dataclass, field
import html
from http.client import HTTPException
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import time
from urllib import error, request


REPOSITORY = 'collaborationwithothers/eventhubs-kafka-dotnet'
MAIN = 'refs/heads/main'
RULESET_ID = 24907776
API = f'https://api.github.com/repos/{REPOSITORY}'
RULES_URL = f'{API}/rules/branches/main?per_page=100&page=1'
RULESET_URL = f'{API}/rulesets/{RULESET_ID}'
REQUIRED_RULES = {'deletion', 'non_fast_forward'}
BRANCH_LIMIT = 100
RUN_SECONDS = 900
GIT_SECONDS = 60
HTTP_SECONDS = 10
HTTP_BYTES = 1048576
PROTECTION_REASON = 'main protection could not be established'
STATUSES = ('deleted', 'retained_unmerged', 'retained_changed', 'retained_error', 'deferred_limit')


class CleanupError(Exception):
    """An operation could not establish the contract's safe deletion conditions."""


class OverallTimeout(CleanupError):
    pass


class ProtectionError(CleanupError):
    pass


@dataclass
class Deadline:
    clock: object = time.monotonic
    end: float = field(init=False)

    def __post_init__(self):
        self.end = self.clock() + RUN_SECONDS

    def remaining(self, maximum):
        remaining = self.end - self.clock()
        if remaining <= 0:
            raise OverallTimeout('overall cleanup deadline reached')
        return min(maximum, remaining)


@dataclass
class Row:
    branch: str
    head: str
    status: str = ''
    preserved_by: str = ''
    reason: str = ''


@dataclass
class Report:
    rows: list = field(default_factory=list)
    errors: list = field(default_factory=list)

    @property
    def exit_code(self):
        return int(bool(self.errors) or any(row.status == 'retained_error' for row in self.rows))

    def render(self, secrets=()):
        def escape(value):
            for secret in secrets:
                if secret:
                    value = value.replace(secret, '[redacted]')
            value = html.escape(value, quote=False)
            value = re.sub(r'[\\`*_[\]{}()#+.!|\-]', lambda match: f'&#{ord(match[0])};', value)
            return value.replace('\n', '&#10;').replace('\r', '&#13;')

        lines = ['# Backup branch cleanup', '', '| Branch | Head | Status | Preserved by | Reason |',
                 '| --- | --- | --- | --- | --- |']
        for row in self.rows:
            lines.append('| ' + ' | '.join(escape(value) for value in (
                row.branch, row.head, row.status, row.preserved_by, row.reason)) + ' |')
        totals = Counter(row.status for row in self.rows)
        lines += ['', f'Enumerated total: {len(self.rows)}']
        lines += [f'{status}: {totals[status]}' for status in STATUSES]
        lines += ['', *(f'Error: {escape(message)}' for message in self.errors)] if self.errors else ['']
        return '\n'.join(lines) + '\n'


class Git:
    def __init__(self, directory, deadline, runner=subprocess.run):
        self.directory = directory
        self.deadline = deadline
        self.runner = runner

    def run(self, arguments, operation, allowed=(0,)):
        try:
            result = self.runner(['git', *arguments], cwd=self.directory, capture_output=True,
                                 text=True, timeout=self.deadline.remaining(GIT_SECONDS))
        except subprocess.TimeoutExpired:
            self.deadline.remaining(GIT_SECONDS)
            raise CleanupError(f'{operation} timed out') from None
        except OSError:
            raise CleanupError(f'{operation} could not start') from None
        self.deadline.remaining(GIT_SECONDS)
        if result.returncode not in allowed:
            # Git's stderr can contain authentication or transport details.
            raise CleanupError(f'{operation} failed (exit {result.returncode})')
        return result

    def heads(self, refs=()):
        result = self.run(['ls-remote', '--heads', '--refs', 'origin', *refs], 'listing remote heads')
        heads = {}
        for line in result.stdout.splitlines():
            fields = line.split('\t')
            if len(fields) != 2 or not re.fullmatch(r'(?:[0-9a-f]{40}|[0-9a-f]{64})', fields[0]):
                raise CleanupError('listing remote heads returned malformed data')
            head, ref = fields
            if not ref.startswith('refs/heads/') or ref in heads:
                raise CleanupError('listing remote heads returned malformed data')
            heads[ref] = head
        return heads

    def ancestor(self, head, main):
        shallow = self.run(['rev-parse', '--is-shallow-repository'], 'checking complete history').stdout.strip()
        if shallow != 'false':
            raise CleanupError('complete history is unavailable (shallow repository)')
        self.run(['fetch', '--no-tags', '--no-write-fetch-head', 'origin', head, main], 'fetching complete history')
        self.run(['rev-list', '--parents', head, main], 'checking complete history')
        result = self.run(['merge-base', '--is-ancestor', head, main], 'checking ancestry', (0, 1))
        return result.returncode == 0

    def delete(self, branch, head):
        return self.run(['-c', 'push.followTags=false', 'push',
                         f'--force-with-lease={branch}:{head}', 'origin', f':{branch}'],
                        'deleting backup')


class NoRedirect(request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


@contextmanager
def http_deadline(seconds):
    """Bound the whole HTTP exchange, including headers and response reads."""
    def expired(signum, frame):
        raise TimeoutError('HTTP deadline reached')

    previous_handler = signal.signal(signal.SIGALRM, expired)
    previous_timer = signal.setitimer(signal.ITIMER_REAL, seconds)
    started = time.monotonic()
    try:
        yield
    finally:
        signal.setitimer(signal.ITIMER_REAL, 0)
        signal.signal(signal.SIGALRM, previous_handler)
        if previous_timer[0] > 0:
            signal.setitimer(signal.ITIMER_REAL, max(0.000001, previous_timer[0] - (time.monotonic() - started)),
                             previous_timer[1])


class Protection:
    def __init__(self, token, deadline, opener=None):
        self.token = token
        self.deadline = deadline
        self.opener = opener if opener is not None else request.build_opener(NoRedirect())

    def read(self, url):
        if not self.token:
            raise ProtectionError(PROTECTION_REASON)
        timeout = self.deadline.remaining(HTTP_SECONDS)
        req = request.Request(url, method='GET', headers={
            'Accept': 'application/vnd.github+json',
            'Authorization': f'Bearer {self.token}',
            'X-GitHub-Api-Version': '2026-03-10',
        })
        try:
            with http_deadline(timeout), self.opener.open(req, timeout=timeout) as response:
                if response.status != 200 or response.geturl() != url:
                    raise ProtectionError(PROTECTION_REASON)
                data = response.read(HTTP_BYTES + 1)
                if len(data) > HTTP_BYTES:
                    raise ProtectionError(PROTECTION_REASON)
                result = json.loads(data)
        except (HTTPException, error.URLError, OSError, TimeoutError, ValueError):
            raise ProtectionError(PROTECTION_REASON) from None
        self.deadline.remaining(HTTP_SECONDS)
        return result

    def check(self):
        effective = self.read(RULES_URL)
        ruleset = self.read(RULESET_URL)
        if not isinstance(effective, list) or not all(isinstance(rule, dict) for rule in effective):
            raise ProtectionError(PROTECTION_REASON)
        present = {rule.get('type') for rule in effective
                   if type(rule.get('ruleset_id')) is int and rule['ruleset_id'] == RULESET_ID
                   and rule.get('ruleset_source_type') == 'Repository'
                   and rule.get('ruleset_source') == REPOSITORY and isinstance(rule.get('type'), str)}
        if not REQUIRED_RULES <= present or not isinstance(ruleset, dict):
            raise ProtectionError(PROTECTION_REASON)
        if (type(ruleset.get('id')) is not int or ruleset['id'] != RULESET_ID
                or ruleset.get('target') != 'branch' or ruleset.get('enforcement') != 'active'
                or ruleset.get('conditions') != {'ref_name': {'include': ['~DEFAULT_BRANCH'], 'exclude': []}}):
            raise ProtectionError(PROTECTION_REASON)
        rules = ruleset.get('rules')
        actors = ruleset.get('bypass_actors')
        if (not isinstance(rules, list) or not all(isinstance(rule, dict) for rule in rules)
                or not REQUIRED_RULES <= {rule.get('type') for rule in rules if isinstance(rule.get('type'), str)}
                or not isinstance(actors, list)
                or not all(isinstance(actor, dict) and actor.get('bypass_mode') == 'pull_request' for actor in actors)):
            raise ProtectionError(PROTECTION_REASON)


def cleanup(git, protection, deadline):
    """The test seam takes dependencies, never a production remote override."""
    report = Report()
    try:
        heads = git.heads()
        report.rows = [Row(ref, head) for ref, head in sorted(heads.items())
                       if ref.startswith('refs/heads/') and ref.endswith('-wip')]
        selected = report.rows[:BRANCH_LIMIT]
        for row in report.rows[BRANCH_LIMIT:]:
            row.status, row.reason = 'deferred_limit', '100-branch processing limit reached'
        protection.check()
        main = heads.get(MAIN)
        if main is None:
            raise CleanupError('main is missing from remote heads')
        for row in selected:
            deadline.remaining(GIT_SECONDS)
            try:
                if not git.ancestor(row.head, main):
                    row.status, row.reason = 'retained_unmerged', 'head is not an ancestor of protected main'
                    continue
                current = git.heads((MAIN, row.branch))
                if current.get(row.branch) != row.head or current.get(MAIN) != main:
                    row.status, row.reason = 'retained_changed', 'backup or main changed or disappeared before deletion'
                    continue
                protection.check()
                try:
                    git.delete(row.branch, row.head)
                except CleanupError as failure:
                    if isinstance(failure, OverallTimeout):
                        raise
                    current = git.heads((row.branch,))
                    if current.get(row.branch) != row.head:
                        row.status, row.reason = 'retained_changed', 'backup changed or disappeared during deletion'
                        continue
                    raise failure
                row.status, row.preserved_by = 'deleted', f'{MAIN} at {main}'
                row.reason = 'head is an ancestor of protected main'
            except (OverallTimeout, ProtectionError):
                raise
            except CleanupError as failure:
                row.status, row.reason = 'retained_error', str(failure)
    except CleanupError as failure:
        reason = PROTECTION_REASON if isinstance(failure, ProtectionError) else str(failure)
        report.errors.append(reason)
        for row in report.rows:
            if not row.status:
                row.status, row.reason = 'retained_error', reason
    return report


def validate_context(git, environment):
    if environment.get('GITHUB_REPOSITORY') != REPOSITORY or environment.get('GITHUB_REF') != MAIN:
        raise CleanupError('cleanup requires this repository on refs/heads/main')
    if sys.version_info < (3, 12) or shutil.which('git') is None:
        raise CleanupError('cleanup requires Python 3.12 or newer and Git')
    allowed = {f'https://github.com/{REPOSITORY}', f'https://github.com/{REPOSITORY}.git'}
    for option in ([], ['--push']):
        urls = git.run(['remote', 'get-url', *option, '--all', 'origin'], 'checking origin URL').stdout.splitlines()
        if not urls or any(url not in allowed for url in urls):
            raise CleanupError('origin URL does not match this repository')


def main():
    environment = os.environ
    deadline = Deadline()
    git = Git(Path(__file__).resolve().parents[1], deadline)
    try:
        if len(sys.argv) != 1:
            raise CleanupError('cleanup accepts no arguments')
        validate_context(git, environment)
        report = cleanup(git, Protection(environment.get('GITHUB_TOKEN', ''), deadline), deadline)
    except CleanupError as failure:
        report = Report(errors=[str(failure)])
    secrets = tuple(environment.get(name, '') for name in ('GITHUB_TOKEN', 'GH_TOKEN'))
    summary = environment.get('GITHUB_STEP_SUMMARY')
    if summary:
        try:
            with Path(summary).open('a', encoding='utf-8') as stream:
                stream.write(report.render(secrets))
        except OSError:
            report.errors.append('could not write run summary')
    print(report.render(secrets), end='')
    return report.exit_code


if __name__ == '__main__':
    raise SystemExit(main())

#!/usr/bin/env python3
"""Bounded configured-positive-grant inventory for scientific role-scope probes."""
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import resource
import signal
import subprocess
import sys
import time
from urllib.parse import urlsplit

from probe_v2 import decode, private_json, unwrap

ACCOUNTS = ('loadgen', 'enrichment-consumer')
TARGETS = tuple(sorted(('document-events', 'document-state', 'document-events-dlq')))
ARM = 'https://management.azure.com'
GRAPH = 'https://graph.microsoft.com'
VERSION = '2022-04-01'
MODE = 'azure_rbac_configuration_check'
GUID = r'[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}'
NAMESPACE = rf'/subscriptions/{GUID}/resourceGroups/[A-Za-z0-9_.()-]+/providers/Microsoft.EventHub/namespaces/[A-Za-z0-9-]+'
ROLE_ID = rf'(?:/(?:subscriptions/{GUID}|providers/Microsoft.Management/managementGroups/[A-Za-z0-9_.()-]+)(?:/resourceGroups/[A-Za-z0-9_.()-]+)?)?/providers/Microsoft.Authorization/roleDefinitions/{GUID}'
ROLE_GUIDS = {'sender': '2b629674-e913-4c01-ae53-ef4638d8f975', 'receiver': 'a638d3c7-ab3a-418d-83e6-5f17a39d4fde'}
OPERATIONS = {'sender': 'send', 'receiver': 'receive'}
LIMITS = dict(audit_seconds=600, requests=256, pages_per_collection=8, groups=64,
              definitions=64, assignments=4096, private_files=1024, response_bytes=1048576,
              total_raw_bytes=16777216, projection_bytes=32768, automatic_retries=0, first_admission_age_ms=60000)
INTENDED = {('loadgen', 'document-events', 'send'), ('enrichment-consumer', 'document-events', 'receive'),
            ('enrichment-consumer', 'document-state', 'send'), ('enrichment-consumer', 'document-events-dlq', 'send')}


class Gap(Exception):
    def __init__(self, reason):
        self.reason = reason
        super().__init__(reason)


def encoded(value):
    return json.dumps(value, separators=(',', ':'), ensure_ascii=True).encode('utf-8')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def alias(kind, identity):
    return kind + ':' + sha(identity.lower().encode())


def validate_binding(binding):
    if set(binding) != {'run_id', 'source_commit', 'namespace_id', 'principals', 'targets'}:
        raise Gap('input_invalid')
    if not isinstance(binding['run_id'], str) or not re.fullmatch(r'[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?', binding['run_id']):
        raise Gap('input_invalid')
    if not isinstance(binding['source_commit'], str) or not re.fullmatch('[0-9a-f]{40}', binding['source_commit']):
        raise Gap('input_invalid')
    ns = binding['namespace_id']
    if not isinstance(ns, str) or not re.fullmatch(NAMESPACE, ns, flags=re.I):
        raise Gap('input_invalid')
    if set(binding['principals']) != set(ACCOUNTS) or any(not isinstance(v, str) or not re.fullmatch(GUID, v) for v in binding['principals'].values()):
        raise Gap('input_invalid')
    if len(set(v.lower() for v in binding['principals'].values())) != 2 or set(binding['targets']) != set(TARGETS):
        raise Gap('input_invalid')
    if any(not isinstance(v, str) or v.lower() != (ns + '/eventhubs/' + k).lower() for k, v in binding['targets'].items()):
        raise Gap('input_invalid')


def binding_hash(binding):
    fields = ['probe-v2-access-v1', binding['run_id'], binding['source_commit'], binding['namespace_id'].lower()]
    fields += [k + '=' + binding['principals'][k].lower() for k in ACCOUNTS]
    fields += [k + '=' + binding['targets'][k].lower() for k in TARGETS]
    return sha(('\n'.join(fields) + '\n').encode())


def scope_class(scope, binding):
    """Classify only validated resource relationships; unknown paths stay unsupported."""
    if not isinstance(scope, str) or not scope.startswith('/') or any(c in scope for c in '?#%\\\r\n'):
        raise Gap('assignment_shape')
    value = scope.lower()
    ns = binding['namespace_id'].lower()
    targets = {k: v.lower() for k, v in binding['targets'].items()}
    if value in targets.values():
        return 'event_hub', [k for k, v in targets.items() if v == value]
    descendants = [k for k, v in targets.items() if value.startswith(v + '/consumergroups/') and re.fullmatch(r'[a-z0-9_.()-]+', value[len(v + '/consumergroups/'):])]
    if descendants:
        return 'consumer_group', descendants
    if value == ns:
        return 'namespace', list(TARGETS)
    rg = ns.split('/providers/')[0]
    if value == rg:
        return 'resource_group', list(TARGETS)
    subscription = '/'.join(ns.split('/')[:3])
    if value == subscription:
        return 'subscription', list(TARGETS)
    if re.fullmatch(r'/providers/microsoft.management/managementgroups/[a-z0-9_.()-]+', value):
        return 'management_group', list(TARGETS)
    if value.startswith(ns + '/eventhubs/') and re.fullmatch(r'[a-z0-9_.()-]+(?:/consumergroups/[a-z0-9_.()-]+)?', value[len(ns + '/eventhubs/'):]):
        return 'other', []
    # A different subscription, resource group or service cannot contain our hubs.
    if re.fullmatch(r'/subscriptions/' + GUID + r'(?:/resourcegroups/[a-z0-9_.()-]+)?(?:/providers/[a-z0-9.]+(?:/[a-z0-9_.()-]+/[a-z0-9_.()-]+)+)?', value):
        if not ns.startswith(value + '/') and not value.startswith(ns + '/'):
            return 'other', []
    return 'other', None


def definition_kind(document, identity):
    if not isinstance(document, dict) or str(document.get('id', '')).lower() != identity.lower():
        raise Gap('definition_shape')
    p = document.get('properties')
    if not isinstance(p, dict) or not isinstance(p.get('roleName'), str) or not isinstance(p.get('type'), str):
        raise Gap('definition_shape')
    blocks = p.get('permissions')
    if not isinstance(blocks, list) or not blocks:
        raise Gap('definition_shape')
    for block in blocks:
        if not isinstance(block, dict) or any(not isinstance(block.get(k), list) or any(not isinstance(v, str) for v in block[k]) for k in ('actions', 'notActions', 'dataActions', 'notDataActions')):
            raise Gap('definition_shape')
    data = [v for block in blocks for v in block['dataActions']]
    def unrelated(operation):
        prefix = operation.split('/', 1)[0]
        return bool(re.fullmatch(r'Microsoft\.[A-Za-z0-9]+', prefix)) and prefix.lower() != 'microsoft.eventhub'
    if not data or all(unrelated(v) for v in data):
        return 'irrelevant'
    role = next((k for k, v in ROLE_GUIDS.items() if identity.lower().endswith('/' + v)), None)
    if role and p['type'] == 'BuiltInRole' and len(blocks) == 1 and blocks[0]['dataActions'] == ['Microsoft.EventHub/*/' + OPERATIONS[role] + '/action'] and blocks[0]['notDataActions'] == []:
        return role
    if p['type'] != 'BuiltInRole':
        return 'custom_role'
    return 'definition_shape' if role else 'unfamiliar_operation'


def normalize(binding, inventory, started, completed, requests):
    """Normalize retained raw pages without Azure calls or operator assertions."""
    validate_binding(binding)
    reasons = set(inventory['reasons'])
    coverage = {'accounts': {}, 'targets': list(TARGETS), 'definitions': []}
    entries, unsupported, definitions = [], [], {}
    seen_assignments = {}
    aliases = {}
    def checked_alias(kind, identity):
        value = alias(kind, identity)
        original = (kind, identity.lower())
        if value in aliases and aliases[value] != original:
            raise Gap('input_invalid')
        aliases[value] = original
        return value
    group_union = set()
    for account in ACCOUNTS:
        member = inventory['memberships'][account]
        groups, directory = [], []
        for raw in member['pages']:
            page = decode(raw, LIMITS['response_bytes'])
            for obj in page['value']:
                identity = obj.get('id') if isinstance(obj, dict) else None
                kind = obj.get('@odata.type') if isinstance(obj, dict) else None
                if not isinstance(identity, str) or not re.fullmatch(GUID, identity):
                    reasons.add('membership_shape'); continue
                if kind == '#microsoft.graph.group':
                    value = checked_alias('group', identity)
                    if value in groups:
                        reasons.add('membership_shape')
                    groups.append(value); group_union.add(value)
                    if len(groups) > LIMITS['groups'] or len(group_union) > LIMITS['groups']:
                        raise Gap('group_limit')
                elif kind == '#microsoft.graph.directoryRole':
                    value = checked_alias('directory_role', identity)
                    if value in directory:
                        reasons.add('membership_shape')
                    directory.append(value)
                    if len(directory) > LIMITS['groups']:
                        raise Gap('group_limit')
                else:
                    reasons.add('membership_shape')
        if len(groups) > LIMITS['groups'] or len(directory) > LIMITS['groups'] or len(group_union) > LIMITS['groups']:
            raise Gap('group_limit')
        collections = inventory['assignments'][account]
        account_coverage = {'membership_complete': member['complete'], 'membership_pages': [sha(p) for p in member['pages']],
                            'groups': sorted(set(groups)), 'directory_roles': sorted(set(directory)), 'assignment_collections': []}
        coverage['accounts'][account] = account_coverage
        for subject, collection in collections.items():
            subject_alias = account if subject.lower() == binding['principals'][account].lower() else checked_alias('group', subject)
            account_coverage['assignment_collections'].append({'subject': subject_alias, 'complete': collection['complete'], 'pages': [sha(p) for p in collection['pages']]})
            for raw in collection['pages']:
                for assignment in decode(raw, LIMITS['response_bytes'])['value']:
                    if not isinstance(assignment, dict) or not isinstance(assignment.get('properties'), dict):
                        reasons.add('assignment_shape'); continue
                    identity, p = assignment.get('id'), assignment['properties']
                    if not isinstance(identity, str) or not re.fullmatch(r'/[A-Za-z0-9_.()/ -]+/providers/Microsoft.Authorization/roleAssignments/' + GUID, identity, re.I) or str(p.get('principalId', '')).lower() != subject.lower():
                        reasons.add('assignment_shape'); continue
                    identity = identity.lower()
                    if identity in seen_assignments:
                        # Identical cross-account group collections are expected; duplicate within a collection is rejected by the collector.
                        if seen_assignments[identity] != assignment:
                            reasons.add('assignment_shape')
                    seen_assignments[identity] = assignment
                    definition_id = p.get('roleDefinitionId')
                    if not isinstance(definition_id, str) or not re.fullmatch(ROLE_ID, definition_id, re.I):
                        reasons.add('assignment_shape'); continue
                    try:
                        category, targets = scope_class(p.get('scope'), binding)
                    except Gap as gap:
                        reasons.add(gap.reason); continue
                    if targets == []:
                        continue
                    raw_definition = inventory['definitions'].get(definition_id.lower())
                    if raw_definition is None:
                        reasons.add('definition_unavailable'); continue
                    try:
                        kind = definition_kind(decode(raw_definition, LIMITS['response_bytes']), definition_id)
                        category, targets = scope_class(p.get('scope'), binding)
                    except Gap as gap:
                        reasons.add(gap.reason); continue
                    if kind == 'irrelevant' or targets == []:
                        continue
                    definitions[definition_id.lower()] = {'alias': checked_alias('role', definition_id), 'raw_sha256': sha(raw_definition)}
                    unsupported_reason = None
                    if targets is None:
                        unsupported_reason = 'unmatched_scope'
                    elif p.get('condition') not in (None, '') or p.get('conditionVersion') not in (None, ''):
                        unsupported_reason = 'condition'
                    elif p.get('delegatedManagedIdentityResourceId') not in (None, ''):
                        unsupported_reason = 'delegation'
                    elif kind not in OPERATIONS:
                        unsupported_reason = kind
                    elif category != 'event_hub':
                        unsupported_reason = 'unexpected_grant'
                    if unsupported_reason:
                        unsupported.append({'account': account, 'subject': subject_alias, 'scope_class': category, 'reason': unsupported_reason, 'raw_sha256': sha(encoded(assignment))})
                        if unsupported_reason == 'unexpected_grant':
                            reasons.add('configuration_mismatch')
                        else:
                            reasons.add('unsupported_grant')
                        continue
                    target = targets[0]
                    operation = OPERATIONS[kind]
                    entry = {'account': account, 'subject': subject_alias, 'target': target, 'operation': operation, 'role': kind,
                             'assignment_alias': checked_alias('assignment', identity), 'definition_alias': checked_alias('role', definition_id), 'scope_class': category}
                    if entry in entries:
                        reasons.add('assignment_shape')
                    if len(entries) >= 8192:
                        raise Gap('projection_limit')
                    entries.append(entry)
                    if (account, target, operation) not in INTENDED:
                        unsupported.append({'account': account, 'subject': subject_alias, 'scope_class': category, 'reason': 'unexpected_grant', 'raw_sha256': sha(encoded(assignment))})
                        reasons.add('configuration_mismatch')
    coverage['definitions'] = sorted(definitions.values(), key=lambda x: x['alias'])
    actual = {(e['account'], e['target'], e['operation']) for e in entries}
    complete_coverage = all(c['membership_complete'] and len(c['membership_pages']) >= 1 and
                            {a['subject'] for a in c['assignment_collections']} == {account, *c['groups']} and
                            all(a['complete'] and a['pages'] for a in c['assignment_collections'])
                            for account, c in coverage['accounts'].items())
    if not complete_coverage:
        if not reasons:
            reasons.add('assignment_unavailable')
    elif actual != INTENDED:
        reasons.add('configuration_mismatch')
    status = 'incomplete' if reasons - {'configuration_mismatch'} else 'mismatch' if reasons else 'complete'
    projection = {'schema_version': 1, 'mode': MODE, 'run_id': binding['run_id'], 'source_commit': binding['source_commit'],
                  'started_unix_ms': started, 'completed_unix_ms': completed, 'binding_sha256': binding_hash(binding),
                  'status': status, 'reasons': sorted(reasons), 'observed': {'requests': requests, 'api_versions': {'arm': VERSION, 'graph': 'v1.0'}},
                  'coverage': coverage, 'entries': entries, 'unsupported': unsupported, 'source_limits': dict(LIMITS)}
    if len(coverage['definitions']) > 64:
        raise Gap('definition_limit')
    if len(seen_assignments) > 4096:
        raise Gap('assignment_limit')
    if len(entries) > 8192 or len(unsupported) > 4096 or any(len(encoded(e)) > 1024 for e in entries):
        raise Gap('projection_limit')
    return projection


class Inventory:
    def __init__(self, reader, clock=time.monotonic):
        self.reader, self.clock = reader, clock
        self.end = clock() + LIMITS['audit_seconds']
        self.requests = 0
        self.definitions = {}
        self.assignment_count = 0

    def get(self, url, audience, reason):
        if self.clock() >= self.end:
            raise Gap('time_limit')
        if self.requests >= LIMITS['requests']:
            raise Gap('request_limit')
        self.requests += 1
        try:
            raw = self.reader(url, audience)
            value = decode(raw, LIMITS['response_bytes'])
        except Gap:
            raise
        except (OSError, ValueError, UnicodeError, TypeError):
            raise Gap(reason) from None
        return raw, value

    def pages(self, url, audience, reason, shape):
        result = {'pages': [], 'complete': False}
        origin = urlsplit(url)
        links, ids = set(), {}
        while url:
            parts = urlsplit(url)
            if (parts.scheme != 'https' or parts.netloc != origin.netloc or parts.path != origin.path or parts.username or parts.password or parts.fragment or url in links):
                raise Gap('pagination_invalid')
            if len(result['pages']) >= LIMITS['pages_per_collection']:
                raise Gap('page_limit')
            links.add(url)
            raw, page = self.get(url, audience, reason)
            if not isinstance(page, dict) or not isinstance(page.get('value'), list):
                raise Gap(shape)
            if shape == 'assignment_shape':
                self.assignment_count += len(page['value'])
                if self.assignment_count > LIMITS['assignments']:
                    raise Gap('assignment_limit')
            for obj in page['value']:
                identity = obj.get('id') if isinstance(obj, dict) else None
                if not isinstance(identity, str) or identity.lower() in ids:
                    raise Gap(shape)
                ids[identity.lower()] = obj
            result['pages'].append(raw)
            key = '@odata.nextLink' if audience == GRAPH else 'nextLink'
            link = page.get(key)
            if link is not None and (not isinstance(link, str) or not link):
                raise Gap('pagination_invalid')
            url = link
        result['complete'] = True
        return result

    def collect(self, binding):
        validate_binding(binding)
        result = {'memberships': {}, 'assignments': {}, 'definitions': self.definitions, 'reasons': set()}
        groups_union = set()
        for account in ACCOUNTS:
            result['memberships'][account] = {'pages': [], 'complete': False}
            result['assignments'][account] = {}
        try:
            for account in ACCOUNTS:
                principal = binding['principals'][account]
                member = self.pages(GRAPH + '/v1.0/servicePrincipals/' + principal + '/transitiveMemberOf', GRAPH, 'membership_unavailable', 'membership_shape')
                result['memberships'][account] = member
                groups, directory = [], []
                for raw in member['pages']:
                    for obj in decode(raw, LIMITS['response_bytes'])['value']:
                        if not isinstance(obj.get('id'), str) or not re.fullmatch(GUID, obj['id']):
                            raise Gap('membership_shape')
                        if obj.get('@odata.type') == '#microsoft.graph.group':
                            groups.append(obj['id']); groups_union.add(obj['id'].lower())
                        elif obj.get('@odata.type') == '#microsoft.graph.directoryRole':
                            directory.append(obj['id'])
                        else:
                            raise Gap('membership_shape')
                if len(groups_union) > LIMITS['groups'] or len(directory) > LIMITS['groups']:
                    raise Gap('group_limit')
                for subject in [principal, *groups]:
                    url = ARM + binding['namespace_id'] + '/providers/Microsoft.Authorization/roleAssignments?api-version=' + VERSION + '&$filter=principalId%20eq%20%27' + subject + '%27'
                    assignments = self.pages(url, ARM, 'assignment_unavailable', 'assignment_shape')
                    result['assignments'][account][subject] = assignments
                    for raw in assignments['pages']:
                        for assignment in decode(raw, LIMITS['response_bytes'])['value']:
                            p = assignment.get('properties')
                            if not isinstance(p, dict):
                                raise Gap('assignment_shape')
                            # Ignore a scope only when its validated relationship excludes all targets.
                            _, affected = scope_class(p.get('scope'), binding)
                            if affected == []:
                                continue
                            identity = p.get('roleDefinitionId')
                            if not isinstance(identity, str) or not re.fullmatch(ROLE_ID, identity, re.I):
                                raise Gap('assignment_shape')
                            if identity.lower() not in self.definitions:
                                if len(self.definitions) >= LIMITS['definitions']:
                                    raise Gap('definition_limit')
                                document, _ = self.get(ARM + identity + '?api-version=' + VERSION, ARM, 'definition_unavailable')
                                self.definitions[identity.lower()] = document
        except Gap as gap:
            result['reasons'].add(gap.reason)
        return result


def materialize(binding, projection):
    raw = encoded(projection)
    if len(raw) > LIMITS['projection_bytes']:
        raise Gap('projection_limit')
    envelope = {'schema_version': 1, 'mode': MODE, **binding,
                'started_unix_ms': projection['started_unix_ms'], 'completed_unix_ms': projection['completed_unix_ms'],
                'projection_base64': base64.b64encode(raw).decode(), 'projection_sha256': sha(raw)}
    if len(encoded(envelope)) > 131072:
        raise Gap('projection_limit')
    return envelope, raw


def gap_projection(binding, reason, started, completed, requests):
    inventory = {'reasons': {reason}, 'definitions': {},
                 'memberships': {a: {'pages': [], 'complete': False} for a in ACCOUNTS}, 'assignments': {a: {} for a in ACCOUNTS}}
    return normalize(binding, inventory, started, completed, requests)


def collect(binding, reader, wall_clock=lambda: int(time.time() * 1000), clock=time.monotonic):
    started = wall_clock()
    inventory = Inventory(reader, clock)
    data = inventory.collect(binding)
    completed = wall_clock()
    observed_requests = getattr(reader, 'requests', inventory.requests)
    try:
        projection = normalize(binding, data, started, completed, observed_requests)
        return materialize(binding, projection)
    except (Gap, ValueError, KeyError, TypeError, UnicodeError) as error:
        reason = error.reason if isinstance(error, Gap) else 'input_invalid'
        return materialize(binding, gap_projection(binding, reason, started, completed, observed_requests))


class ProductionReader:
    def __init__(self, private, repo, clock=time.monotonic, command=None):
        self.directory = Path(private) / 'probe-v2/access-raw'
        self.directory.mkdir(mode=0o700, parents=True, exist_ok=False)
        self.repo, self.clock = Path(repo), clock
        self.end = clock() + LIMITS['audit_seconds']
        self.files, self.raw_bytes, self.requests = 0, 0, 0
        self.command = command
        self.unsafe = False

    def __call__(self, url, audience):
        if self.unsafe:
            raise Gap('safety_stop')
        remaining = self.end - self.clock()
        if remaining <= 5:
            raise Gap('time_limit')
        if self.files + 3 > LIMITS['private_files']:
            raise Gap('private_file_limit')
        ceiling = min(LIMITS['response_bytes'], (LIMITS['total_raw_bytes'] - self.raw_bytes) // 2)
        if ceiling <= 0:
            raise Gap('raw_bytes_limit')
        stdout = self.directory / (str(self.files) + '.json')
        stderr = self.directory / (str(self.files + 1) + '.stderr')
        self.files += 3
        seconds = min(60, remaining - 5)
        argv = self.command or ['az', 'rest', '--method', 'get', '--url', url, '--resource', audience, '--output', 'json', '--only-show-errors']
        command = [sys.executable, str(self.repo / 'infra/scripts/lifecycle/command.py'), str(seconds), '--stdout-file', str(stdout), '--max-bytes', str(ceiling), '--', *argv]
        process = None
        previous = {}
        cancelled = []
        def interrupted(number, _frame):
            cancelled.append(number)
            if process is not None:
                process.send_signal(number)
        def file_limit():
            resource.setrlimit(resource.RLIMIT_FSIZE, (ceiling, ceiling))
        try:
            fd = os.open(stderr, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
            with os.fdopen(fd, 'wb') as error_stream:
                previous = {sig: signal.signal(sig, interrupted) for sig in (signal.SIGTERM, signal.SIGINT)}
                process = subprocess.Popen(command, stdout=subprocess.DEVNULL, stderr=error_stream, preexec_fn=file_limit)
                self.requests += 1
                try:
                    code = process.wait(timeout=max(0.001, remaining))
                except subprocess.TimeoutExpired:
                    process.terminate()
                    try:
                        process.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        self.unsafe = True
                        process.kill(); process.wait()
                    raise Gap('safety_stop' if self.unsafe else 'time_limit') from None
                error_stream.flush()
            sizes = [p.stat().st_size if p.exists() else 0 for p in (stdout, stderr)]
            self.raw_bytes += sum(sizes)
            if cancelled:
                raise Gap('safety_stop')
            if code == 125 or code != 0 and any(size >= ceiling for size in sizes):
                raise Gap('response_limit')
            if code == 124:
                raise Gap('time_limit')
            if code != 0 or Path(str(stdout) + '.incomplete').exists():
                raise Gap('transport_failure')
            return stdout.read_bytes()
        except OSError:
            self.unsafe = process is not None and process.poll() is None
            if self.unsafe:
                process.terminate()
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill(); process.wait()
            raise Gap('safety_stop' if self.unsafe else 'transport_failure') from None
        finally:
            for sig, handler in previous.items():
                signal.signal(sig, handler)


def main():
    if len(sys.argv) != 2:
        print('permission_evidence_incomplete'); return 1
    private = Path(sys.argv[1])
    try:
        phase = decode((private / 'phase.json').read_bytes(), 131072)
        ephemeral = decode((private / 'ephemeral-output.json').read_bytes(), 1048576)
        cluster = decode((private / 'cluster-output.json').read_bytes(), 1048576)
        scope = unwrap(ephemeral['probe_v2_role_scope'])
        identities = unwrap(cluster['workload_identity'])['service_accounts']
        binding = {'run_id': phase['run_id'], 'source_commit': phase['source_commit'], 'namespace_id': scope['namespace_id'],
                   'principals': {a: scope['accounts'][a]['principal_id'] for a in ACCOUNTS}, 'targets': scope['topics']}
        validate_binding(binding)
        if any(binding['principals'][a].lower() != identities[a]['principal_id'].lower() for a in ACCOUNTS):
            raise Gap('input_invalid')
        if not sys.platform.startswith('linux'):
            now = int(time.time() * 1000)
            envelope, raw = materialize(binding, gap_projection(binding, 'platform_unsupported', now, now, 0))
        else:
            envelope, raw = collect(binding, ProductionReader(private, Path(__file__).resolve().parents[3]))
        private_json(private / 'probe-v2/access-check.json', envelope)
        artifact = Path(os.environ['PARCHWELL_ARTIFACT_DIRECTORY']) / 'probe-v2-access-check.json'
        artifact.write_bytes(raw)
        projection = decode(raw, LIMITS['projection_bytes'])
        print(json.dumps({'permission_check': projection['status'], 'reasons': projection['reasons']}, separators=(',', ':')))
        return 0 if projection['status'] == 'complete' else 1
    except (OSError, Gap, ValueError, TypeError, KeyError, UnicodeError):
        print('permission_evidence_incomplete'); return 1


if __name__ == '__main__':
    sys.exit(main())

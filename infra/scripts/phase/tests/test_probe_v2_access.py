"""Synthetic Graph/ARM pages exercise the production inventory and byte producer."""
import base64
import contextlib
import copy
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import MagicMock, patch

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO / 'infra/scripts/phase'))
import probe_v2_access as audit

NS = '/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/synthetic-rg/providers/Microsoft.EventHub/namespaces/synthetic-ns'
BINDING = {'run_id': 'run1', 'source_commit': '1' * 40, 'namespace_id': NS,
           'principals': {'loadgen': '11111111-1111-1111-1111-111111111111', 'enrichment-consumer': '33333333-3333-3333-3333-333333333333'},
           'targets': {t: NS + '/eventhubs/' + t for t in audit.TARGETS}}
GROUP = '55555555-5555-5555-5555-555555555555'
ROLE_ROOT = '/subscriptions/11111111-1111-1111-1111-111111111111/providers/Microsoft.Authorization/roleDefinitions/'


def definition(role='sender'):
    return {'id': ROLE_ROOT + audit.ROLE_GUIDS[role], 'properties': {'type': 'BuiltInRole', 'roleName': 'Azure Event Hubs Data ' + role.title(),
        'permissions': [{'actions': [], 'notActions': [], 'dataActions': ['Microsoft.EventHub/*/' + audit.OPERATIONS[role] + '/action'], 'notDataActions': []}]}}


def assignment(account, target, role, number=1, subject=None):
    return {'id': NS + '/providers/Microsoft.Authorization/roleAssignments/' + f'{number:08x}-0000-0000-0000-000000000000',
            'properties': {'principalId': subject or BINDING['principals'][account], 'scope': BINDING['targets'][target],
                           'roleDefinitionId': ROLE_ROOT + audit.ROLE_GUIDS[role], 'condition': None, 'conditionVersion': None}}


def fixtures():
    return {'loadgen': [assignment('loadgen', 'document-events', 'sender', 1)],
            'enrichment-consumer': [assignment('enrichment-consumer', 'document-events', 'receiver', 2),
                                    assignment('enrichment-consumer', 'document-state', 'sender', 3),
                                    assignment('enrichment-consumer', 'document-events-dlq', 'sender', 4)]}


class Reader:
    def __init__(self, grants=None, memberships=None, definitions=None):
        self.grants = fixtures() if grants is None else grants
        self.memberships = memberships or {}
        self.definitions = definitions or {d['id'].lower(): d for d in (definition(), definition('receiver'))}
        self.calls = []
        self.hook = None

    def __call__(self, url, audience):
        self.calls.append((url, audience))
        if self.hook:
            result = self.hook(url, audience)
            if result is not None:
                return result
        if audience == audit.GRAPH:
            principal = url.split('/servicePrincipals/')[1].split('/')[0]
            return audit.encoded({'value': self.memberships.get(principal, [])})
        if '/roleDefinitions/' in url:
            identity = url.split('?')[0][len(audit.ARM):].lower()
            return audit.encoded(self.definitions[identity])
        principal = url.split('%27')[1]
        account = next((a for a, v in BINDING['principals'].items() if v == principal), principal)
        return audit.encoded({'value': self.grants.get(account, [])})


def produce(reader=None):
    return audit.collect(copy.deepcopy(BINDING), reader or Reader(), wall_clock=iter((1000, 2000)).__next__)


class AccessInventoryTests(unittest.TestCase):
    def projection(self, reader=None):
        envelope, raw = produce(reader)
        self.assertEqual(base64.b64decode(envelope['projection_base64']), raw)
        self.assertEqual(envelope['projection_sha256'], hashlib.sha256(raw).hexdigest())
        return json.loads(raw)

    def test_actual_producer_export(self):
        envelope, raw = produce()
        projection = json.loads(raw)
        self.assertEqual(projection['status'], 'complete')
        self.assertEqual(projection['reasons'], [])
        self.assertEqual(projection['observed']['requests'], 6)
        self.assertEqual(projection['observed']['api_versions'], {'arm': '2022-04-01', 'graph': 'v1.0'})
        self.assertEqual(len(projection['entries']), 4)
        self.assertEqual(projection['coverage']['targets'], list(audit.TARGETS))
        self.assertEqual(set(envelope), set('schema_version mode run_id source_commit namespace_id principals targets started_unix_ms completed_unix_ms projection_base64 projection_sha256'.split()))
        for account in audit.ACCOUNTS:
            c = projection['coverage']['accounts'][account]
            self.assertTrue(c['membership_complete'])
            self.assertEqual(c['groups'], [])
            self.assertEqual(len(c['membership_pages']), 1)
            self.assertEqual(len(c['assignment_collections']), 1)
        destination = os.environ.get('PARCHWELL_V2_ACCESS_CONTRACT_EXPORT')
        if destination:
            path = Path(destination); path.mkdir(parents=True, exist_ok=True)
            (path / 'access-check.json').write_bytes(audit.encoded(envelope))
            (path / 'probe-v2-access-check.json').write_bytes(raw)

    def test_tenant_level_builtin_definition_ids_are_resolved(self):
        grants = fixtures()
        definitions = {}
        for role in ('sender', 'receiver'):
            document = definition(role)
            document['id'] = '/providers/Microsoft.Authorization/roleDefinitions/' + audit.ROLE_GUIDS[role]
            definitions[document['id'].lower()] = document
        for rows in grants.values():
            for row in rows:
                row['properties']['roleDefinitionId'] = '/providers/Microsoft.Authorization/roleDefinitions/' + row['properties']['roleDefinitionId'].rsplit('/', 1)[1]
        reader = Reader(grants=grants, definitions=definitions)
        value = self.projection(reader)
        self.assertEqual('complete', value['status'])
        self.assertEqual([], value['reasons'])
        self.assertEqual(6, value['observed']['requests'])
        self.assertEqual(4, len(value['entries']))
        for document in definitions.values():
            self.assertIn((audit.ARM + document['id'] + '?api-version=' + audit.VERSION, audit.ARM), reader.calls)

    def test_tenant_definition_path_suffix_injection_is_rejected_before_get(self):
        for suffix in ('?injected=true', '#fragment', '/extra'):
            with self.subTest(suffix=suffix):
                grants = fixtures()
                grants['loadgen'][0]['properties']['roleDefinitionId'] = '/providers/Microsoft.Authorization/roleDefinitions/' + audit.ROLE_GUIDS['sender'] + suffix
                reader = Reader(grants=grants)
                value = self.projection(reader)
                self.assertEqual('incomplete', value['status'])
                self.assertEqual(['assignment_shape'], value['reasons'])
                self.assertFalse(any('/roleDefinitions/' in url for url, _ in reader.calls))

    def test_nested_group_memberships_and_directory_roles(self):
        grants = fixtures()
        grants[GROUP] = [assignment('loadgen', 'document-events', 'sender', 7, GROUP)]
        groups = {BINDING['principals']['loadgen']: [{'id': GROUP, '@odata.type': '#microsoft.graph.group'},
            {'id': '66666666-6666-6666-6666-666666666666', '@odata.type': '#microsoft.graph.directoryRole'}]}
        value = self.projection(Reader(grants, groups))
        self.assertEqual(value['status'], 'complete')
        self.assertEqual(value['coverage']['accounts']['loadgen']['groups'], [audit.alias('group', GROUP)])
        self.assertEqual(len(value['coverage']['accounts']['loadgen']['directory_roles']), 1)
        self.assertEqual(len(value['entries']), 5)

    def test_unavailable_and_malformed_membership(self):
        for response, reason in ((b'{"value":[] , "value":[]}', 'membership_unavailable'), (b'{"value":NaN}', 'membership_unavailable'),
                                 (b'{"value":{}}', 'membership_shape'), (b'{"value":[{}]}', 'membership_shape')):
            with self.subTest(response=response):
                reader = Reader(); reader.hook = lambda u, a: response if a == audit.GRAPH else None
                value = self.projection(reader)
                self.assertEqual(value['status'], 'incomplete'); self.assertEqual(value['reasons'], [reason])
                self.assertEqual(len(reader.calls), 1)

    def test_read_denied_no_retries(self):
        for kind, expected in (('membership', 'membership_unavailable'), ('assignment', 'assignment_unavailable'), ('definition', 'definition_unavailable')):
            reader = Reader()
            def denied(url, audience):
                if (kind == 'membership' and audience == audit.GRAPH or kind == 'assignment' and '/roleAssignments?' in url or kind == 'definition' and '/roleDefinitions/' in url):
                    raise OSError('synthetic private unauthorized response')
            reader.hook = denied
            value = self.projection(reader)
            self.assertEqual(value['status'], 'incomplete'); self.assertEqual(value['reasons'], [expected])
            self.assertEqual(len(reader.calls), len(set(reader.calls)))

    def test_page_links_and_final_empty_page(self):
        reader = Reader(); first = audit.GRAPH + '/v1.0/servicePrincipals/' + BINDING['principals']['loadgen'] + '/transitiveMemberOf'
        reader.hook = lambda u, a: audit.encoded({'value': [], '@odata.nextLink': first + '?opaque=next'}) if u == first else None
        value = self.projection(reader)
        self.assertEqual(value['status'], 'complete')
        self.assertEqual(len(value['coverage']['accounts']['loadgen']['membership_pages']), 2)
        self.assertIn((first + '?opaque=next', audit.GRAPH), reader.calls)

    def test_invalid_and_repeated_continuations(self):
        for next_link in ('http://graph.microsoft.com/v1.0/test', 'https://evil.test/v1.0/test',
                          'https://graph.microsoft.com/other', 'https://user@graph.microsoft.com/v1.0/test',
                          'https://graph.microsoft.com/v1.0/test#fragment', 'same'):
            reader = Reader()
            def page(url, audience):
                if audience == audit.GRAPH:
                    return audit.encoded({'value': [], '@odata.nextLink': url if next_link == 'same' else next_link})
            reader.hook = page
            self.assertEqual(self.projection(reader)['reasons'], ['pagination_invalid'])
            self.assertEqual(len(reader.calls), 1)

    def test_supported_extra_grant_and_broader_scopes(self):
        for scope in (BINDING['targets']['document-state'], NS, '/'.join(NS.split('/')[:3]),
                      NS.split('/providers/')[0], '/providers/Microsoft.Management/managementGroups/synthetic-parent',
                      BINDING['targets']['document-events'] + '/consumergroups/synthetic-group'):
            grants = fixtures(); item = assignment('loadgen', 'document-state', 'sender', 8); item['properties']['scope'] = scope; grants['loadgen'].append(item)
            value = self.projection(Reader(grants))
            self.assertEqual(value['status'], 'mismatch'); self.assertEqual(value['reasons'], ['configuration_mismatch'])
            if scope != BINDING['targets']['document-state']:
                self.assertEqual(value['unsupported'][0]['reason'], 'unexpected_grant')

    def test_unsupported_grants_and_definition_spoof(self):
        for mutation in ('condition', 'delegation', 'custom', 'name-spoof', 'operation', 'not-data'):
            grants, definitions = fixtures(), {d['id'].lower(): d for d in (definition(), definition('receiver'))}
            if mutation == 'condition': grants['loadgen'][0]['properties']['condition'] = 'private expression'
            elif mutation == 'delegation': grants['loadgen'][0]['properties']['delegatedManagedIdentityResourceId'] = NS
            elif mutation == 'custom': definitions[(ROLE_ROOT + audit.ROLE_GUIDS['sender']).lower()]['properties']['type'] = 'CustomRole'
            elif mutation == 'name-spoof': definitions[(ROLE_ROOT + audit.ROLE_GUIDS['sender']).lower()]['id'] = ROLE_ROOT + '77777777-7777-7777-7777-777777777777'
            elif mutation == 'operation': definitions[(ROLE_ROOT + audit.ROLE_GUIDS['sender']).lower()]['properties']['permissions'][0]['dataActions'] = ['*/send/action']
            else: definitions[(ROLE_ROOT + audit.ROLE_GUIDS['sender']).lower()]['properties']['permissions'][0]['notDataActions'] = ['Microsoft.EventHub/*/send/action']
            value = self.projection(Reader(grants, definitions=definitions))
            self.assertEqual(value['status'], 'incomplete')
            self.assertTrue(set(value['reasons']) & {'unsupported_grant', 'definition_shape'})

    def test_storage_irrelevant_and_notdata_not_global_deny(self):
        grants = fixtures(); irrelevant = assignment('loadgen', 'document-events', 'sender', 8)
        identity = ROLE_ROOT + '77777777-7777-7777-7777-777777777777'; irrelevant['properties']['roleDefinitionId'] = identity
        grants['loadgen'].append(irrelevant)
        storage = definition(); storage['id'] = identity
        storage['properties']['permissions'][0]['dataActions'] = ['Microsoft.Storage/*']
        storage['properties']['permissions'][0]['notDataActions'] = ['Microsoft.EventHub/*/send/action']
        definitions = {d['id'].lower(): d for d in (definition(), definition('receiver'), storage)}
        value = self.projection(Reader(grants, definitions=definitions))
        self.assertEqual(value['status'], 'complete'); self.assertEqual(len(value['entries']), 4)

    def test_duplicate_objects_unknown_type_injection(self):
        for mutation in ('duplicate', 'unknown', 'definition-query', 'scope-query'):
            grants = fixtures(); groups = {}
            if mutation == 'duplicate': grants['loadgen'].append(copy.deepcopy(grants['loadgen'][0]))
            elif mutation == 'unknown': groups[BINDING['principals']['loadgen']] = [{'id': GROUP, '@odata.type': '#microsoft.graph.unknown'}]
            elif mutation == 'definition-query': grants['loadgen'][0]['properties']['roleDefinitionId'] += '?leak=private'
            else: grants['loadgen'][0]['properties']['scope'] += '?leak=private'
            self.assertEqual(self.projection(Reader(grants, groups))['status'], 'incomplete')

    def test_binding_input_rejected_before_query(self):
        for field, value in (('run_id', 'private\nvalue'), ('source_commit', 'wrong'), ('namespace_id', NS + '?query')):
            binding = copy.deepcopy(BINDING); binding[field] = value; reader = Reader()
            with self.assertRaises(audit.Gap): audit.collect(binding, reader)
            self.assertEqual(reader.calls, [])

    def test_budgets_requests_pages_groups_definitions_assignments(self):
        for limit, cap, expected in (('requests', 0, 'request_limit'), ('pages_per_collection', 0, 'page_limit'),
                                     ('groups', 0, 'group_limit'), ('definitions', 0, 'definition_limit'), ('assignments', 0, 'assignment_limit')):
            reader = Reader(memberships={BINDING['principals']['loadgen']: [{'id': GROUP, '@odata.type': '#microsoft.graph.group'}]})
            with patch.dict(audit.LIMITS, {limit: cap}):
                # The wire records fixed production limits; tests only lower the admission policy.
                value = self.projection(reader)
                self.assertEqual(value['status'], 'incomplete')
                self.assertIn(expected, value['reasons'])

    def test_projection_limit_gap_not_truncation(self):
        with patch.dict(audit.LIMITS, {'projection_bytes': 10}):
            with self.assertRaises(audit.Gap): produce()

    def test_raw_identifiers_not_in_public_projection(self):
        _, raw = produce()
        for identity in (NS, *BINDING['principals'].values(), ROLE_ROOT, 'https://', 'Azure Event Hubs'):
            self.assertNotIn(identity.encode(), raw)


class ProductionReaderTests(unittest.TestCase):
    def run_reader(self, script, policy=None):
        with tempfile.TemporaryDirectory() as directory:
            reader = audit.ProductionReader(directory, REPO, command=[sys.executable, '-c', script])
            if policy:
                with patch.dict(audit.LIMITS, policy):
                    result = reader('https://private.synthetic/secret', audit.ARM)
            else:
                result = reader('https://private.synthetic/secret', audit.ARM)
            return result

    def test_actual_subprocess_output_stays_private(self):
        with tempfile.TemporaryDirectory() as directory:
            reader = audit.ProductionReader(directory, REPO, command=[sys.executable, '-c', 'import sys; print("{\\"value\\":[]}"); print("synthetic-private-token",file=sys.stderr)'])
            raw = reader('https://private.synthetic/secret', audit.ARM)
            self.assertEqual(json.loads(raw), {'value': []})
            files = list((Path(directory) / 'probe-v2/access-raw').iterdir())
            self.assertTrue(all(p.stat().st_mode & 0o777 == 0o600 for p in files))
            self.assertIn('synthetic-private-token', next(p for p in files if p.suffix == '.stderr').read_text())

    def test_both_streams_capped(self):
        for stream in ('stdout', 'stderr'):
            with self.subTest(stream=stream), self.assertRaises(audit.Gap) as caught:
                self.run_reader('import sys; sys.' + stream + '.write("x"*100000)', {'response_bytes': 1024})
            self.assertEqual(caught.exception.reason, 'response_limit')

    def test_file_and_byte_budgets_checked_before_launch(self):
        for field, value, reason in (('private_files', 2, 'private_file_limit'), ('total_raw_bytes', 0, 'raw_bytes_limit')):
            with self.assertRaises(audit.Gap) as caught:
                self.run_reader('raise Exception("must not run")', {field: value})
            self.assertEqual(caught.exception.reason, reason)

    def test_deadline_nested_subprocess_cleanup(self):
        with tempfile.TemporaryDirectory() as directory:
            reader = audit.ProductionReader(directory, REPO, command=[sys.executable, '-c', 'import time; time.sleep(30)'])
            reader.end = reader.clock() + 5.1
            with self.assertRaises(audit.Gap) as caught:
                reader('https://private.synthetic/secret', audit.ARM)
            self.assertEqual(caught.exception.reason, 'time_limit')

class AccessBoundaryTests(unittest.TestCase):
    def test_runtime_counts_below_at_above_policy(self):
        for key, actual, reason in (('requests', 6, 'request_limit'), ('pages_per_collection', 1, 'page_limit'),
                                    ('definitions', 2, 'definition_limit'), ('assignments', 4, 'assignment_limit')):
            for policy in (actual - 1, actual, actual + 1):
                with self.subTest(key=key, policy=policy), patch.dict(audit.LIMITS, {key: policy}):
                    value = json.loads(produce()[1])
                    self.assertEqual(value['status'], 'incomplete' if policy < actual else 'complete')
                    if policy < actual: self.assertIn(reason, value['reasons'])

    def test_groups_below_at_above(self):
        for policy in (0, 1, 2):
            reader = Reader(memberships={BINDING['principals']['loadgen']: [{'id': GROUP, '@odata.type': '#microsoft.graph.group'}]})
            with self.subTest(policy=policy), patch.dict(audit.LIMITS, {'groups': policy}):
                value = json.loads(produce(reader)[1])
                self.assertEqual(value['status'], 'incomplete' if policy == 0 else 'complete')

    def test_multipage_collection_boundaries(self):
        first = audit.GRAPH + '/v1.0/servicePrincipals/' + BINDING['principals']['loadgen'] + '/transitiveMemberOf'
        for pages in (7, 8, 9):
            reader = Reader()
            def paged(url, audience):
                if url.startswith(first):
                    number = int(url.split('?page=')[1]) if '?page=' in url else 1
                    body = {'value': []}
                    if number < pages: body['@odata.nextLink'] = first + '?page=' + str(number + 1)
                    return audit.encoded(body)
            reader.hook = paged
            value = json.loads(produce(reader)[1])
            self.assertEqual(value['status'], 'incomplete' if pages > 8 else 'complete')
            if pages > 8: self.assertEqual(value['reasons'], ['page_limit'])

    def test_time_boundary(self):
        for elapsed, accepted in ((599.999, True), (600, False), (600.001, False)):
            reader = Reader(); timer = iter((0, elapsed)).__next__
            inventory = audit.Inventory(reader, timer)
            if accepted:
                # Use a fixed successful GET to isolate only the audit clock predicate.
                inventory.reader = lambda u, a: b'{}'
                inventory.get(audit.GRAPH + '/test', audit.GRAPH, 'membership_unavailable')
                self.assertEqual(inventory.requests, 1)
            else:
                with self.assertRaises(audit.Gap) as caught: inventory.get(audit.GRAPH + '/test', audit.GRAPH, 'membership_unavailable')
                self.assertEqual(caught.exception.reason, 'time_limit'); self.assertEqual(reader.calls, [])

    def test_alias_collision_rejected(self):
        reader = Reader(memberships={BINDING['principals']['loadgen']: [{'id': GROUP, '@odata.type': '#microsoft.graph.group'},
            {'id': '66666666-6666-6666-6666-666666666666', '@odata.type': '#microsoft.graph.group'}]})
        with patch.object(audit, 'alias', lambda kind, identity: kind + ':' + 'a' * 64):
            value = json.loads(produce(reader)[1])
        self.assertEqual(value['status'], 'incomplete'); self.assertEqual(value['reasons'], ['input_invalid'])

    def test_conflicting_assignment_copies_and_missing_principal(self):
        for field, value in (('principalId', GROUP), ('scope', None), ('roleDefinitionId', None)):
            grants = fixtures(); grants['loadgen'][0]['properties'][field] = value
            result = json.loads(produce(Reader(grants))[1])
            self.assertEqual(result['status'], 'incomplete'); self.assertIn('assignment_shape', result['reasons'])

    def test_unrelated_scope_and_unknown_scope(self):
        for scope, status in ((NS + '/eventhubs/not-target', 'complete'), ('/unrecognized/private', 'incomplete')):
            grants = fixtures(); other = assignment('loadgen', 'document-state', 'sender', 9); other['properties']['scope'] = scope; grants['loadgen'].append(other)
            self.assertEqual(json.loads(produce(Reader(grants))[1])['status'], status)


class ProductionBoundaryTests(unittest.TestCase):
    def test_stdout_byte_boundary(self):
        for size in (1023, 1024, 1025):
            with self.subTest(size=size), tempfile.TemporaryDirectory() as directory, patch.dict(audit.LIMITS, {'response_bytes': 1024}):
                reader = audit.ProductionReader(directory, REPO, command=[sys.executable, '-c', f'import sys; sys.stdout.write("x"*{size})'])
                if size <= 1024:
                    self.assertEqual(len(reader('https://private.synthetic', audit.ARM)), size)
                else:
                    with self.assertRaises(audit.Gap) as caught: reader('https://private.synthetic', audit.ARM)
                    self.assertEqual(caught.exception.reason, 'response_limit')

    def test_private_file_admission_boundary(self):
        for cap in (2, 3, 4):
            with self.subTest(cap=cap), tempfile.TemporaryDirectory() as directory, patch.dict(audit.LIMITS, {'private_files': cap}):
                reader = audit.ProductionReader(directory, REPO, command=[sys.executable, '-c', 'print("{}")'])
                if cap < 3:
                    with self.assertRaises(audit.Gap) as caught: reader('https://private.synthetic', audit.ARM)
                    self.assertEqual(caught.exception.reason, 'private_file_limit')
                else:
                    self.assertEqual(json.loads(reader('https://private.synthetic', audit.ARM)), {})

    def test_real_failing_stderr_sink_remains_private(self):
        with tempfile.TemporaryDirectory() as directory:
            reader = audit.ProductionReader(directory, REPO, command=[sys.executable, '-c', 'print("{}")'])
            def failing(descriptor, mode):
                os.close(descriptor)
                return open('/dev/full', mode)
            with patch.object(audit.os, 'fdopen', failing), self.assertRaises(audit.Gap) as caught:
                reader('https://private.synthetic/token', audit.ARM)
            self.assertEqual(caught.exception.reason, 'transport_failure')

    def test_signal_propagates_to_nested_command_and_child_cleanup(self):
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            script = directory / 'reader.py'
            child_code = 'import os,time,pathlib; pathlib.Path(' + repr(str(directory / 'child-pid')) + ').write_text(str(os.getpid())); time.sleep(30)'
            script.write_text('''import sys, pathlib, os, json
sys.path.insert(0, sys.argv[1])
import probe_v2_access as audit
root=pathlib.Path(sys.argv[2])
command=[sys.executable,'-c', ''' + repr(child_code) + ''']
reader=audit.ProductionReader(root,sys.argv[3],command=command)
try: reader('https://private.synthetic/token', audit.ARM)
except audit.Gap as gap: (root/'result').write_text(gap.reason)
''')
            process = subprocess.Popen([sys.executable, str(script), str(REPO / 'infra/scripts/phase'), str(directory), str(REPO)], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            import time
            deadline = time.monotonic() + 5
            while not (directory / 'child-pid').exists() and time.monotonic() < deadline:
                time.sleep(0.01)
            self.assertTrue((directory / 'child-pid').exists())
            process.terminate()
            stdout, stderr = process.communicate(timeout=8)
            self.assertEqual(stdout, b''); self.assertEqual(stderr, b'')
            self.assertEqual((directory / 'result').read_text(), 'safety_stop')
            pid = int((directory / 'child-pid').read_text())
            self.assertFalse(Path('/proc/' + str(pid)).exists())


# The files mirror the terraform output -json shapes of infra/ephemeral, infra/cluster and infra/persistent outputs.tf, with synthetic GUIDs.
class MainTests(unittest.TestCase):
    ACCOUNT_NAMES = ('loadgen', 'probe', 'enrichment-consumer', 'verifier')
    COMPLETE = '{"permission_check":"complete","reasons":[]}\n'
    INCOMPLETE = 'permission_evidence_incomplete\n'

    @classmethod
    def files(cls):
        principals = {'loadgen': BINDING['principals']['loadgen'], 'enrichment-consumer': BINDING['principals']['enrichment-consumer'],
                      'probe': '22222222-2222-2222-2222-222222222222', 'verifier': '44444444-4444-4444-4444-444444444444'}
        clients = {a: '0000000%d-0000-0000-0000-000000000000' % n for n, a in enumerate(cls.ACCOUNT_NAMES, 1)}
        return {
            'phase.json': {'scenario': 'probe-batch-2', 'phase': 'single-action', 'configuration': None, 'run_id': 'run1',
                           'source_commit': '1' * 40, 'run_kind': 'batch', 'items': ['B08', 'E01', 'F06']},
            'ephemeral-output.json': {'probe_v2_role_scope': {'sensitive': True, 'type': 'object', 'value': {
                'namespace_id': NS, 'topics': dict(BINDING['targets']),
                'accounts': {a: {'client_id': clients[a], 'principal_id': principals[a]} for a in cls.ACCOUNT_NAMES},
                'assignments': {}, 'probe_namespace_data_roles': True}}},
            'cluster-output.json': {'workload_identity': {'sensitive': False, 'type': 'object', 'value': {
                'namespace': 'parchwell',
                'service_accounts': {k: {'name': k, 'client_id': clients[k], 'subject': 'system:serviceaccount:parchwell:' + k}
                                     for k in cls.ACCOUNT_NAMES}}}},
            'persistent-output.json': {
                'acr': {'sensitive': False, 'type': 'object', 'value': {
                    'id': '/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/synthetic-rg/providers/Microsoft.ContainerRegistry/registries/parchwellacr',
                    'name': 'parchwellacr', 'login_server': 'parchwellacr.azurecr.io'}},
                'application_insights_connection_string': {'sensitive': True, 'type': 'string',
                                                           'value': 'InstrumentationKey=00000000-0000-0000-0000-000000000001'},
                'workload_identities': {'sensitive': False, 'type': 'object', 'value': {
                    k: {'id': '/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/synthetic-rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-' + k,
                        'client_id': clients[k], 'principal_id': principals[k]} for k in cls.ACCOUNT_NAMES}}}}

    def run_main(self, files, factory=None):
        with tempfile.TemporaryDirectory() as root:
            private = Path(root) / 'private'
            artifacts = Path(root) / 'artifacts'
            private.mkdir(); artifacts.mkdir()
            for name, value in files.items():
                if value is not None:
                    (private / name).write_text(json.dumps(value) + '\n')
            factory = factory or MagicMock(side_effect=lambda private_dir, repo: Reader())
            out = io.StringIO()
            with patch.object(sys, 'argv', ['probe_v2_access.py', str(private)]), \
                    patch.dict(os.environ, {'PARCHWELL_ARTIFACT_DIRECTORY': str(artifacts)}), \
                    patch.object(audit, 'ProductionReader', factory), contextlib.redirect_stdout(out):
                code = audit.main()
            return (code, out.getvalue(), (private / 'probe-v2/access-check.json').exists(),
                    (artifacts / 'probe-v2-access-check.json').exists(), factory)

    def test_main_with_real_output_shapes(self):
        code, out, private_written, artifact_written, _ = self.run_main(self.files())
        self.assertEqual(code, 0)
        self.assertEqual(out, self.COMPLETE)
        self.assertTrue(private_written)
        self.assertTrue(artifact_written)

    def test_main_principal_cross_check(self):
        def upper(f):
            ids = f['persistent-output.json']['workload_identities']['value']
            ids['loadgen']['principal_id'] = ids['loadgen']['principal_id'].upper()

        def different(f):
            f['persistent-output.json']['workload_identities']['value']['enrichment-consumer']['principal_id'] = '99999999-9999-9999-9999-999999999999'

        def none(f):
            f['persistent-output.json']['workload_identities']['value']['loadgen']['principal_id'] = None

        def entry_missing(f):
            del f['persistent-output.json']['workload_identities']['value']['loadgen']

        def key_absent(f):
            del f['persistent-output.json']['workload_identities']

        def file_absent(f):
            f['persistent-output.json'] = None

        def identities_not_mapping(f):
            f['persistent-output.json']['workload_identities']['value'] = ['not', 'a', 'mapping']

        def identity_not_mapping(f):
            f['persistent-output.json']['workload_identities']['value']['loadgen'] = 'not-a-mapping'

        cases = (('uppercased', upper, 0, self.COMPLETE), ('different', different, 1, self.INCOMPLETE),
                 ('none', none, 1, self.INCOMPLETE), ('entry_missing', entry_missing, 1, self.INCOMPLETE),
                 ('key_absent', key_absent, 1, self.INCOMPLETE), ('file_absent', file_absent, 1, self.INCOMPLETE),
                 ('identities_not_mapping', identities_not_mapping, 1, self.INCOMPLETE),
                 ('identity_not_mapping', identity_not_mapping, 1, self.INCOMPLETE))
        for name, mutate, expected_code, expected_out in cases:
            with self.subTest(name):
                files = self.files()
                mutate(files)
                code, out, private_written, artifact_written, factory = self.run_main(files)
                self.assertEqual((code, out), (expected_code, expected_out))
                if expected_code:
                    factory.assert_not_called()
                    self.assertFalse(private_written)
                    self.assertFalse(artifact_written)

    def test_main_persistent_output_byte_limit(self):
        for size in (4194304, 4194305, 4194306):
            with self.subTest(size=size):
                files = self.files()
                persistent = files['persistent-output.json']
                persistent['padding'] = ''
                base = len((json.dumps(persistent) + '\n').encode())
                persistent['padding'] = 'x' * (size - base)
                self.assertEqual(len((json.dumps(persistent) + '\n').encode()), size)
                code, out, private_written, artifact_written, factory = self.run_main(files)
                if size <= 4194305:
                    self.assertEqual((code, out), (0, self.COMPLETE))
                else:
                    self.assertEqual((code, out), (1, self.INCOMPLETE))
                    factory.assert_not_called()
                    self.assertFalse(private_written)
                    self.assertFalse(artifact_written)


if __name__ == '__main__':
    unittest.main()

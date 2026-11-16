"""Streaming, depth-bounded JSON projection for large source publications."""
import base64
import hashlib
import io
import json
import math
import os
import re
import time


class Invalid(ValueError):
    pass


class Reader:
    def __init__(self, path, deadline, sink=None):
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
        self.stream = io.TextIOWrapper(os.fdopen(fd, 'rb'), encoding='utf-8', errors='strict', newline='')
        self.buffer = ''; self.offset = 0; self.deadline = deadline; self.sink = sink; self.suppress = False

    def close(self): self.stream.close()

    def peek(self):
        if self.offset == len(self.buffer):
            if time.monotonic() >= self.deadline: raise TimeoutError()
            self.buffer = self.stream.read(65536); self.offset = 0
        return self.buffer[self.offset:self.offset + 1]

    def take(self):
        value = self.peek()
        if not value: raise Invalid()
        self.offset += 1
        if self.sink is not None and not self.suppress: self.sink.write(value.encode('utf-8'))
        return value

    def whitespace(self):
        while self.peek() in (' ', '\t', '\r', '\n'): self.take()

    def string(self, keep):
        if self.take() != '"': raise Invalid()
        if not keep:
            while True:
                if not self.peek(): raise Invalid()
                match = re.search(r'["\\\x00-\x1f]', self.buffer[self.offset:])
                end = len(self.buffer) if match is None else self.offset + match.start()
                chunk = self.buffer[self.offset:end]
                if self.sink is not None and not self.suppress and chunk: self.sink.write(chunk.encode('utf-8'))
                self.offset = end
                if match is None: continue
                ch = self.take()
                if ch == '"': return None
                if ord(ch) < 32: raise Invalid()
                escaped = self.take()
                if escaped == 'u':
                    for _ in range(4):
                        if self.take() not in '0123456789abcdefABCDEF': raise Invalid()
                elif escaped not in '"\\/bfnrt': raise Invalid()
        chars = ['"'] if keep else None
        while True:
            ch = self.take()
            if ord(ch) < 32: raise Invalid()
            if chars is not None:
                chars.append(ch)
                if len(chars) > 131072: raise Invalid()
            if ch == '"': break
            if ch == '\\':
                escaped = self.take()
                if chars is not None: chars.append(escaped)
                if escaped == 'u':
                    for _ in range(4):
                        digit = self.take()
                        if digit not in '0123456789abcdefABCDEF': raise Invalid()
                        if chars is not None: chars.append(digit)
                elif escaped not in '"\\/bfnrt': raise Invalid()
        if chars is None: return None
        try: return json.loads(''.join(chars))
        except (ValueError, UnicodeError): raise Invalid() from None

    def body(self):
        self.whitespace(); self.suppress = True
        try:
            if self.peek() == 'n':
                if ''.join(self.take() for _ in range(4)) != 'null': raise Invalid()
                metadata = {'is_null': True, 'length': None, 'sha256': None}
            else:
                if self.take() != '\"': raise Invalid()
                digest = hashlib.sha256(); length = 0; quartet = ''; padded = False; block = bytearray()
                while True:
                    ch = self.take()
                    if ch == '\"': break
                    if ch == '\\':
                        esc = self.take()
                        if esc == 'u':
                            digits = ''.join(self.take() for _ in range(4))
                            if not re.fullmatch(r'[0-9A-Fa-f]{4}', digits): raise Invalid()
                            ch = chr(int(digits, 16))
                        elif esc == '/': ch = '/'
                        else: raise Invalid()
                    if ch not in 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=' or padded:
                        raise Invalid()
                    quartet += ch
                    if len(quartet) == 4:
                        try: decoded = base64.b64decode(quartet, validate=True)
                        except ValueError: raise Invalid() from None
                        block.extend(decoded); length += len(decoded); padded = '=' in quartet; quartet = ''
                        if len(block) >= 65536: digest.update(block); block.clear()
                if quartet: raise Invalid()
                digest.update(block)
                metadata = {'is_null': False, 'length': length, 'sha256': digest.hexdigest()}
        finally:
            self.suppress = False
        if self.sink is not None: self.sink.write(json.dumps(metadata, separators=(',', ':')).encode())
        return metadata

    def raw_statistics(self):
        # Copy the original JSON string while validating its base64 in constant space.
        if self.take() != '"': raise Invalid()
        quartet = ''; length = 0; padded = False
        while True:
            ch = self.take()
            if ch == '"': break
            if ch == '\\':
                esc = self.take()
                if esc == 'u':
                    digits = ''.join(self.take() for _ in range(4))
                    if not re.fullmatch(r'[0-9A-Fa-f]{4}', digits): raise Invalid()
                    ch = chr(int(digits, 16))
                elif esc == '/': ch = '/'
                else: raise Invalid()
            if ch not in 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=' or padded: raise Invalid()
            quartet += ch
            if len(quartet) == 4:
                try: decoded = base64.b64decode(quartet, validate=True)
                except ValueError: raise Invalid() from None
                length += len(decoded); padded = '=' in quartet; quartet = ''
                if length > 262144: raise Invalid()
        if quartet: raise Invalid()
        return ('statistics_base64', length)

    def value(self, depth=1, mode='skip', path=()):
        if depth > 32: raise Invalid()
        self.whitespace(); ch = self.peek()
        if mode == 'scalar' and ch in '{[': raise Invalid()
        if mode == 'matched' and ch not in '{n': raise Invalid()
        if mode == 'shape':
            relative = path[2:]
            objects = {('settings',), ('client_errors', '*'), ('statistics', '*')}
            nullable_objects = {('error',), ('first',), ('second',), ('first', 'error'), ('second', 'error')}
            arrays = {('omitted_keys',), ('client_errors',), ('statistics',), ('diagnostics',)}
            strings = {('settings', '*'), ('omitted_keys', '*'), ('diagnostics', '*'), ('statistics', '*', 'raw_base64')}
            if (relative in objects and ch != '{' or relative in nullable_objects and ch not in '{n'
                    or relative in arrays and ch != '[' or relative in strings and ch != '"'):
                raise Invalid()
            if ch in '{[' and relative not in objects | nullable_objects | arrays: raise Invalid()
            if len(relative) == 2 and relative[0] == 'settings' and ch != '"': raise Invalid()
        if ch == '{':
            self.take(); keys = set(); result = {}; self.whitespace()
            settings = mode == 'shape' and path == ('repeats', '*', 'settings')
            if self.peek() == '}':
                self.take(); return ('settings',) if settings else result if mode != 'skip' else None
            while True:
                self.whitespace(); key = self.string(True)
                if len(key) > 1024 or key in keys or len(keys) >= 100000: raise Invalid()
                keys.add(key); self.whitespace()
                if mode == 'matched' and key not in {'topic', 'partition', 'key_base64', 'value_base64'}: raise Invalid()
                if mode == 'shape':
                    schemas = {
                        ('repeats', '*', 'error'): {'code', 'is_fatal', 'origin', 'reason'},
                        ('repeats', '*', 'client_errors', '*'): {'code', 'is_fatal', 'origin', 'reason', 'observed_elapsed_ms'},
                        ('repeats', '*', 'statistics', '*'): {'sequence', 'observed_elapsed_ms', 'raw_base64'},
                        ('repeats', '*', 'first'): {'attempt', 'key', 'started_elapsed_ms', 'terminal', 'result', 'error', 'terminal_elapsed_ms', 'terminal_unix_ms'},
                        ('repeats', '*', 'second'): {'attempt', 'key', 'started_elapsed_ms', 'terminal', 'result', 'error', 'terminal_elapsed_ms', 'terminal_unix_ms'},
                        ('repeats', '*', 'first', 'error'): {'code', 'is_fatal', 'origin', 'reason'},
                        ('repeats', '*', 'second', 'error'): {'code', 'is_fatal', 'origin', 'reason'},
                    }
                    if path in schemas and key not in schemas[path]: raise Invalid()
                if self.take() != ':': raise Invalid()
                child_mode = ('shape' if mode == 'shape' else
                              'shape' if mode == 'repeat' and key in ('settings', 'omitted_keys', 'result', 'error', 'first', 'second', 'client_errors', 'statistics', 'diagnostics', 'client_started_unix_ms', 'idle_started_elapsed_ms', 'idle_ended_elapsed_ms') else
                              'identity' if mode in ('root', 'windowroot') and key == 'identity' else
                              'repeats' if mode == 'root' and key == 'repeats' else
                              'matched' if mode == 'repeat' and key == 'matched' else
                              'windowdata' if mode == 'windowroot' and key == 'data' else
                              'vector' if mode in ('windowroot', 'windowdata') and key in ('start_line', 'end_line', 'partition_highs') else
                              'scalar' if mode in ('windowroot', 'windowdata') and key in ('topic', 'verifier_commit', 'source_commit', 'run_id', 'scenario_id', 'configuration_id', 'phase', 'schema_version', 'kind', 'dialect', 'purpose') else
                              'scalar' if mode in ('root', 'identity') else
                              'scalar' if mode == 'matched' and key in ('topic', 'partition', 'key_base64') else
                              'scalar' if mode == 'vectoritem' and key in ('partition', 'offset', 'high') else
                              'scalar' if mode == 'repeat' and key in ('repeat', 'client_id', 'action', 'terminal', 'elapsed_ms', 'terminal_unix_ms', 'client_errors_dropped') else 'skip')
                body_path = (path == ('repeats', '*', 'matched') or path == ('repeats', '*', 'requested')
                             or path == ('repeats', '*', 'read_observation', 'record'))
                self.whitespace()
                if settings:
                    if self.peek() != '"': raise Invalid()
                    self.string(False); value = None
                elif mode == 'shape' and key == 'reason' and path in {
                        ('repeats', '*', 'error'), ('repeats', '*', 'client_errors', '*'),
                        ('repeats', '*', 'first', 'error'), ('repeats', '*', 'second', 'error')}:
                    if self.peek() != '"': raise Invalid()
                    self.string(False); value = ('error_reason',)
                elif mode == 'shape' and key == 'raw_base64' and path == ('repeats', '*', 'statistics', '*') and self.peek() == '"':
                    value = self.raw_statistics()
                elif key == 'value_base64' and body_path:
                    value = self.body()
                elif key == 'value_base64' and mode != 'shape' and not (len(path) >= 2 and path[-2:] == ('headers', '*')):
                    raise Invalid()
                else:
                    value = self.value(depth + 1, child_mode, path + (key,))
                if mode != 'skip' and not settings: result[key] = value
                self.whitespace(); separator = self.take()
                if separator == '}': break
                if separator != ',': raise Invalid()
            return ('settings',) if settings else result if mode != 'skip' else None
        if ch == '[':
            self.take(); values = []; count = 0; previous = None; self.whitespace()
            omissions = mode == 'shape' and path == ('repeats', '*', 'omitted_keys')
            if self.peek() == ']':
                self.take(); return ('omitted_keys',) if omissions else values if mode in ('repeats', 'vector', 'shape') else None
            while True:
                value = self.value(depth + 1, 'repeat' if mode == 'repeats' else 'vectoritem' if mode == 'vector' else 'shape' if mode == 'shape' else 'skip', path + ('*',))
                count += 1
                if mode in ('repeats', 'vector'):
                    if count > (5 if mode == 'repeats' else 1024) or not isinstance(value, dict): raise Invalid()
                    values.append(value)
                elif mode == 'shape':
                    limit = {'client_errors': 64, 'statistics': 80, 'diagnostics': 100, 'omitted_keys': 1000000}.get(path[-1], 1000000)
                    if count > limit: raise Invalid()
                    if omissions:
                        if not isinstance(value, str): raise Invalid()
                        order = value.encode('utf-16-be', errors='surrogatepass')
                        if previous is not None and order <= previous: raise Invalid()
                        previous = order
                    else: values.append(value)
                self.whitespace(); separator = self.take()
                if separator == ']': break
                if separator != ',': raise Invalid()
            return ('omitted_keys',) if omissions else values if mode in ('repeats', 'vector', 'shape') else None
        if ch == '"': return self.string(mode != 'skip')
        token = ''
        while self.peek() and self.peek() not in (' ', '\t', '\r', '\n', ',', ']', '}'):
            token += self.take()
            if len(token) > 128: raise Invalid()
        if token in ('true', 'false', 'null'):
            return {'true': True, 'false': False, 'null': None}[token]
        if not re.fullmatch(r'-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?', token): raise Invalid()
        value = float(token) if any(c in token for c in '.eE') else int(token)
        if isinstance(value, float) and not math.isfinite(value): raise Invalid()
        return value


def project(path, deadline, sink=None):
    reader = Reader(path, deadline, sink)
    try:
        value = reader.value(mode='root')
        reader.whitespace()
        if reader.peek() or not isinstance(value, dict): raise Invalid()
        return value
    except (UnicodeError, ValueError, RecursionError):
        raise Invalid() from None
    finally:
        reader.close()


def windows(path, deadline):
    reader = Reader(path, deadline)
    try:
        value = reader.value(mode='windowroot')
        reader.whitespace()
        if reader.peek() or not isinstance(value, dict): raise Invalid()
        return value
    except (UnicodeError, ValueError, RecursionError):
        raise Invalid() from None
    finally:
        reader.close()


def candidate(path, deadline, sink, validate):
    """Validate a bounded V2 candidate and write its body-free projection."""
    if time.monotonic() >= deadline: raise TimeoutError()
    # This is the candidate writer/reader's MaxCandidateBytes, not the archive cap.
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, 'rb') as stream:
        raw = stream.read(16777217)
    if not raw or len(raw) > 16777216: raise Invalid()
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result: raise Invalid()
            result[key] = value
        return result
    try:
        text = raw.decode('utf-8', errors='strict')
        depth = 0; quoted = escaped = False
        for ch in text:
            if quoted:
                if escaped: escaped = False
                elif ch == '\\': escaped = True
                elif ch == '"': quoted = False
            elif ch == '"': quoted = True
            elif ch in '[{':
                depth += 1
                if depth > 32: raise Invalid()
            elif ch in ']}': depth -= 1
        value = json.loads(text, object_pairs_hook=pairs, parse_constant=lambda _: (_ for _ in ()).throw(Invalid()))
        def obj(v, keys):
            if not isinstance(v, dict) or set(v) != set(keys.split()): raise Invalid()
        def arr(v):
            if not isinstance(v, list): raise Invalid()
        def scalar(v, typ, nullable=False, bits=32):
            if nullable and v is None: return
            if type(v) is not typ: raise Invalid()
            if typ is int and not -(1 << (bits - 1)) <= v < (1 << (bits - 1)): raise Invalid()
        def strings(v):
            arr(v)
            for x in v: scalar(x, str)
        def outcome(v):
            if v is None: return
            obj(v, 'result error_code is_fatal origin')
            scalar(v['result'], str); scalar(v['error_code'], int, True); scalar(v['is_fatal'], bool, True); scalar(v['origin'], str, True)
        def reference(v):
            obj(v, 'run_id scenario_id configuration_id stage source_commit job_name pod_uid raw_sha256')
            for x in v.values(): scalar(x, str)
            if not re.fullmatch(r'[0-9a-f]{64}', v['raw_sha256']): raise Invalid()
        def statistics(v):
            if v is None: return
            obj(v, 'sequence native_time native_ts raw_sha256')
            for k in ('sequence', 'native_time', 'native_ts'): scalar(v[k], int, bits=64)
            scalar(v['raw_sha256'], str)
            if not re.fullmatch(r'[0-9a-f]{64}', v['raw_sha256']): raise Invalid()
        def binary(v, nullable=False):
            if v is None and nullable: return None
            scalar(v, str)
            return base64.b64decode(v, validate=True)
        def error(v):
            if v is None: return
            obj(v, 'code is_fatal origin reason observed_elapsed_ms sequence')
            scalar(v['code'], int)
            for k in ('observed_elapsed_ms', 'sequence'): scalar(v[k], int, bits=64)
            scalar(v['is_fatal'], bool)
            for k in ('origin', 'reason'): scalar(v[k], str)
        obj(value, 'schema_version mode synthetic run_id scenario_id configuration_id source_commit managed_version native_version image_digests status repeats deterministic outcome_class adr012_counts diagnostics security_finding item_assessment')
        scalar(value['schema_version'], int); scalar(value['synthetic'], bool)
        for k in ('mode', 'run_id', 'scenario_id', 'configuration_id', 'source_commit', 'status', 'adr012_counts'): scalar(value[k], str)
        for k in ('managed_version', 'native_version'): scalar(value[k], str, True)
        scalar(value['deterministic'], bool, True); scalar(value['security_finding'], bool)
        if not isinstance(value['image_digests'], dict) or any(type(k) is not str or type(v) is not str for k, v in value['image_digests'].items()): raise Invalid()
        if value['status'] not in ('complete_valid', 'invalid', 'incomplete'): raise Invalid()
        strings(value['diagnostics']); outcome(value['outcome_class'])
        assessment = value['item_assessment']
        if assessment is not None:
            obj(assessment, 'verdict hypotheses reason')
            scalar(assessment['verdict'], str, True); scalar(assessment['reason'], str, True)
            if not isinstance(assessment['hypotheses'], dict) or any(type(k) is not str or type(v) is not str for k, v in assessment['hypotheses'].items()): raise Invalid()
        arr(value['repeats'])
        if len(value['repeats']) != 5: raise Invalid()
        for ordinal, r in enumerate(value['repeats'], 1):
            obj(r, 'repeat status reasons outcome_class broker_count statistics_reference semantic_outcome delivery read_observation seed_reference control_references clients')
            scalar(r['repeat'], int)
            if r['repeat'] != ordinal: raise Invalid()
            scalar(r['status'], str)
            if r['status'] not in ('complete_valid', 'invalid', 'incomplete'): raise Invalid()
            strings(r['reasons']); outcome(r['outcome_class']); scalar(r['broker_count'], int, True); statistics(r['statistics_reference'])
            sem = r['semantic_outcome']; obj(sem, 'delivered_partition consumed_partition key_base64 value_kind value_match')
            for k in ('delivered_partition', 'consumed_partition'): scalar(sem[k], int, True)
            binary(sem['key_base64'], True); scalar(sem['value_kind'], str, True); scalar(sem['value_match'], bool, True)
            d = r['delivery']
            if d is not None:
                obj(d, 'topic partition offset persistence_status error observed_elapsed_ms sequence')
                for k in ('topic', 'persistence_status'): scalar(d[k], str)
                scalar(d['partition'], int)
                for k in ('offset', 'observed_elapsed_ms', 'sequence'): scalar(d[k], int, bits=64)
                error(d['error'])
            observation = r['read_observation']
            if observation is not None:
                obj(observation, 'records_seen eof_count record')
                for k in ('records_seen', 'eof_count'): scalar(observation[k], int)
                record = observation['record']
                if record is not None:
                    obj(record, 'topic partition offset key_base64 value_base64 observed_elapsed_ms sequence')
                    scalar(record['topic'], str)
                    scalar(record['partition'], int)
                    for k in ('offset', 'observed_elapsed_ms', 'sequence'): scalar(record[k], int, bits=64)
                    binary(record['key_base64']); body = binary(record['value_base64'], True)
                    record['value_base64'] = {'is_null': body is None, 'length': None if body is None else len(body), 'sha256': None if body is None else hashlib.sha256(body).hexdigest()}
            if r['seed_reference'] is not None: reference(r['seed_reference'])
            arr(r['control_references'])
            for ref in r['control_references']: reference(ref)
            arr(r['clients'])
            for client in r['clients']:
                obj(client, 'client_id stage broker_count statistics_reference')
                scalar(client['client_id'], str); scalar(client['stage'], str); scalar(client['broker_count'], int, True); statistics(client['statistics_reference'])
        if not validate(value): raise Invalid()
        if time.monotonic() >= deadline: raise TimeoutError()
        sink.write(json.dumps(value, ensure_ascii=True, separators=(',', ':'), allow_nan=False).encode())
        return value
    except (ValueError, TypeError, KeyError, UnicodeError, RecursionError):
        raise Invalid() from None

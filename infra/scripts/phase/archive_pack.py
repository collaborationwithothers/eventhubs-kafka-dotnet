"""Offline binding and coverage check for a separately identified evidence pack.

The caller must derive expected contexts from the pack being assessed, independently
of the supplied archive controls. This helper does not select phases or determine
scientific eligibility. Control verification is injected to avoid an import cycle.
"""
import re
from collections import Counter


_RUN_FIELDS = frozenset(('github_run_id', 'github_run_attempt', 'run_id', 'phase',
                         'source_commit', 'scenario_id', 'configuration_id', 'images',
                         'run_kind', 'purpose', 'scientific_admissible'))
_IDENTITY_FIELDS = ('github_run_id', 'github_run_attempt', 'run_id', 'phase', 'source_commit')
_TRIPLE_FIELDS = frozenset(('identity', 'manifest', 'marker', 'receipt'))


def _phase(value):
    return (isinstance(value, str) and len(value) <= 1024
            and re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9-]*', value) is not None)


def _context(value):
    if not isinstance(value, dict) or set(value) != _RUN_FIELDS:
        return False
    images = value['images']
    return (isinstance(value['github_run_id'], str)
            and re.fullmatch(r'[1-9][0-9]*', value['github_run_id']) is not None
            and len(value['github_run_id']) <= 32
            and type(value['github_run_attempt']) is int
            and 0 < value['github_run_attempt'] < 1000000000
            and isinstance(value['run_id'], str) and len(value['run_id']) <= 29
            and re.fullmatch(r'[a-z0-9](?:[a-z0-9-]*[a-z0-9])?', value['run_id']) is not None
            and _phase(value['phase']) and isinstance(value['source_commit'], str)
            and re.fullmatch(r'[0-9a-f]{40}', value['source_commit']) is not None
            and all(isinstance(value[k], str) and value[k].isascii() and 0 < len(value[k]) <= 1024
                    for k in ('scenario_id', 'configuration_id', 'run_kind', 'purpose'))
            and type(value['scientific_admissible']) is bool
            and isinstance(images, dict) and set(images) == {'loadgen', 'verifier', 'probe'}
            and all(ref is None or (isinstance(ref, str) and len(ref) <= 1024
                    and re.fullmatch(r'[a-z0-9.-]+/parchwell/' + worker + r'@sha256:[0-9a-f]{64}', ref))
                    for worker, ref in images.items()))


def _identity(value):
    return (isinstance(value, dict) and set(value) == set(_IDENTITY_FIELDS)
            and isinstance(value['github_run_id'], str)
            and re.fullmatch(r'[1-9][0-9]*', value['github_run_id']) is not None
            and len(value['github_run_id']) <= 32
            and type(value['github_run_attempt']) is int
            and 0 < value['github_run_attempt'] < 1000000000
            and isinstance(value['run_id'], str) and len(value['run_id']) <= 29
            and re.fullmatch(r'[a-z0-9](?:[a-z0-9-]*[a-z0-9])?', value['run_id']) is not None
            and _phase(value['phase']) and isinstance(value['source_commit'], str)
            and re.fullmatch(r'[0-9a-f]{40}', value['source_commit']) is not None)


def _key(identity):
    return tuple(identity[k] for k in _IDENTITY_FIELDS)


def _identities(keys):
    return [dict(zip(_IDENTITY_FIELDS, key)) for key in sorted(keys)]


def verify_pack(expected_contexts, provided_triples, verify_controls):
    """Check independent expected run contexts and recovered archive controls.

    Each triple has exactly identity, manifest, marker, receipt. Identity is the
    full run/phase identity and the controls are bytes. The explicit identity
    identifies coverage even when controls are missing or malformed. Missing
    includes expected identities without exactly one valid matching triple.
    The injected verifier must reject invalid or incomplete controls and return
    the verified manifest. No scientific eligibility policy is applied here.
    """
    result = {'complete': False, 'missing': [], 'extra': [], 'duplicate_expected': [],
              'duplicate_provided': [], 'invalid': []}
    if not isinstance(expected_contexts, list) or not expected_contexts:
        result['invalid'].append('expected_contexts: expected a nonempty list')
        expected_contexts = []
    if not isinstance(provided_triples, list):
        result['invalid'].append('provided_triples: expected a list')
        provided_triples = []
    if not callable(verify_controls):
        result['invalid'].append('verify_controls: expected a callable')

    expected = {}
    expected_keys = []
    for index, context in enumerate(expected_contexts):
        identity = {k: context[k] for k in _IDENTITY_FIELDS if k in context} if isinstance(context, dict) else None
        if _identity(identity):
            key = _key(identity)
            expected_keys.append(key)
            if _context(context):
                expected[key] = context
            else:
                result['invalid'].append(f'expected[{index}]: invalid context')
        else:
            result['invalid'].append(f'expected[{index}]: invalid context')
    expected_counts = Counter(expected_keys)
    result['duplicate_expected'] = _identities(k for k, count in expected_counts.items() if count > 1)

    provided_counts = Counter()
    verified = set()
    for index, triple in enumerate(provided_triples):
        if not isinstance(triple, dict) or not _identity(triple.get('identity')):
            result['invalid'].append(f'provided[{index}]: invalid triple')
            continue
        key = _key(triple['identity'])
        provided_counts[key] += 1
        if (set(triple) != _TRIPLE_FIELDS
                or any(type(triple[k]) is not bytes for k in ('manifest', 'marker', 'receipt'))
                or not callable(verify_controls)):
            result['invalid'].append(f'provided[{index}]: invalid triple')
            continue
        try:
            manifest = verify_controls(triple['manifest'], triple['marker'], triple['receipt'])
            run = manifest['run']
            if not _context(run) or _key(run) != key or key not in expected or run != expected[key]:
                raise ValueError('context mismatch')
        except Exception:
            result['invalid'].append(f'provided[{index}]: invalid controls or context mismatch')
            continue
        verified.add(key)
    result['extra'] = _identities(set(provided_counts) - set(expected_counts))
    result['duplicate_provided'] = _identities(k for k, count in provided_counts.items() if count > 1)
    result['missing'] = _identities(k for k in expected_counts if k not in verified
                                    or provided_counts[k] != 1 or expected_counts[k] != 1)
    result['complete'] = bool(expected_contexts) and not any(result[k] for k in result if k != 'complete')
    return result

#!/usr/bin/env python3
"""Prove the ordinary timeout host's exported bytes cannot enter B10 science."""
import hashlib
import os
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

import test_phase as support

phase = support.phase
REQUIRED_FILES = {
    'producer/pod-first/registration.json',
    'producer/pod-first/completion.json',
    'producer/pod-first/publication/manifest.json',
    'producer/pod-first/publication/ledger.jsonl',
    'producer/pod-first/publication/index.json',
}
MARKER_KEYS = {'dialect', 'purpose', 'scientific_admissible', 'b10_abrupt_diagnostic'}


def read_export(root):
    listing = phase.strict_json((root / 'export.json').read_bytes())
    if not isinstance(listing, dict) or set(listing) != {'files'} or not isinstance(listing['files'], list):
        raise ValueError('export listing invalid')
    names = sorted(path.relative_to(root).as_posix() for path in root.rglob('*')
                   if path.is_file() and path != root / 'export.json')
    entries = listing['files']
    if any(not isinstance(entry, dict) or set(entry) != {'name', 'length', 'sha256'} for entry in entries):
        raise ValueError('export descriptor invalid')
    if [entry['name'] for entry in entries] != names:
        raise ValueError('export listing differs from files')
    if not REQUIRED_FILES <= set(names):
        raise ValueError('required timeout evidence missing')
    exported = {}
    for entry in entries:
        content = (root / entry['name']).read_bytes()
        if type(entry['length']) is not int or entry['length'] != len(content):
            raise ValueError('export length differs: ' + entry['name'])
        if entry['sha256'] != hashlib.sha256(content).hexdigest():
            raise ValueError('export sha256 differs: ' + entry['name'])
        exported[entry['name']] = content
    return exported


def reject_marker_keys(value, name):
    if isinstance(value, dict):
        found = MARKER_KEYS.intersection(value)
        if found:
            raise ValueError('timeout evidence carries diagnostic marker key: ' + name + ':' + sorted(found)[0])
        for item in value.values():
            reject_marker_keys(item, name)
    elif isinstance(value, list):
        for item in value:
            reject_marker_keys(item, name)


class TimeoutEvidenceCases(unittest.TestCase):
    main = support.PhaseTests.main
    prepare = support.PhaseTests.prepare

    def setUp(self):
        support.PhaseTests.setUp(self)
        for name in list(os.environ):
            if name in ('PARCHWELL_PURPOSE', 'PARCHWELL_B10_ABRUPT_DIAGNOSTIC') or name.lower().startswith('b10abrupt__'):
                del os.environ[name]
        os.environ['PARCHWELL_SCENARIO'] = 'B10'
        self.prepare('C1', run='run-1')
        self.p = phase.Phase()

    def assert_trial_rejected(self, method, args):
        for name in ('candidate.json', 'candidate.json.partial'):
            (self.candidate_dir / name).write_bytes(b'not accepted')
        self.output_file.write_text('')
        with patch.object(self.p, 'run') as run:
            with self.assertRaises(phase.Failure) as caught:
                getattr(self.p, method)(*args)
            self.assertEqual('b10_abrupt_diagnostic_not_scientific', str(caught.exception))
            run.assert_not_called()
        for name in ('candidate.json', 'candidate.json.partial'):
            self.assertFalse((self.candidate_dir / name).exists(), name)
        self.assertIn('full_allowed=false', self.output_file.read_text().splitlines())

    def test_frozen_trial_rejects_every_file_and_scientific_entry_before_work(self):
        document = self.p.read('phase.json')
        document.update(purpose='diagnostic_trial', run_kind='smoke', b10_abrupt_diagnostic=True)
        phase.write_json(self.private / 'phase.json', document)
        for name, content in self.exported.items():
            with self.subTest(file=name):
                self.assert_trial_rejected('check_candidate', (content,))
        for method, args in (('prepare', ('C1',)), ('validate_scenario', ()), ('run_repeats', ()),
                             ('judge', ()), ('candidate', ()), ('candidate_download', ())):
            with self.subTest(entry=method):
                self.assert_trial_rejected(method, args)

    def test_ordinary_bytes_have_no_marker_and_fail_candidate_contract(self):
        for name, content in self.exported.items():
            with self.subTest(file=name):
                if name.endswith('.json'):
                    reject_marker_keys(phase.strict_json(content), name)
                elif name.endswith('.jsonl'):
                    for line in content.splitlines():
                        if line.strip():
                            reject_marker_keys(phase.strict_json(line), name)
                expected = 'candidate contract invalid' if content else 'candidate size invalid'
                with self.assertRaises(phase.Failure) as caught:
                    self.p.check_candidate(content)
                self.assertEqual(expected, str(caught.exception))


def main(argv):
    if len(argv) != 1:
        print('b10_timeout_evidence_export_invalid: expected one directory', file=sys.stderr)
        return 1
    root = Path(argv[0])
    if not root.is_dir() or not (root / 'export.json').is_file():
        print('b10_timeout_evidence_export_missing:' + str(root), file=sys.stderr)
        return 1
    try:
        TimeoutEvidenceCases.exported = read_export(root)
        suite = unittest.defaultTestLoader.loadTestsFromTestCase(TimeoutEvidenceCases)
        result = unittest.TextTestRunner(stream=sys.stderr).run(suite)
        if not result.wasSuccessful() or result.skipped:
            return 1
        print('b10_timeout_evidence_rejected')
        return 0
    except (ValueError, OSError, KeyError, TypeError) as error:
        print('b10_timeout_evidence_export_invalid:' + str(error), file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))

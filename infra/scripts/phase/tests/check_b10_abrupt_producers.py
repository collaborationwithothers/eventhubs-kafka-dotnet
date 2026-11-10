"""Require fresh production exports and run the otherwise undiscovered byte tests."""
from pathlib import Path
import sys
import unittest

from test_b10_abrupt import ProducedBytesCases, abrupt


def main(argv):
    if len(argv) != 1:
        return 1
    root = Path(argv[0])
    if not root.is_dir() or not (root / 'export.json').is_file():
        print('b10_abrupt_wire_export_missing:' + str(root), file=sys.stderr)
        return 1
    try:
        expected = {'first/' + name for name in abrupt.FIRST_FILES + abrupt.COHORT_FILES} | {'successor/' + name for name in abrupt.SUCCESSOR_FILES}
        listing = abrupt.decode((root / 'export.json').read_bytes())
        if set(listing) != {'files'} or [entry['name'] for entry in listing['files']] != sorted(expected):
            raise ValueError('export listing differs')
        if {str(path.relative_to(root)).replace('\\', '/') for path in root.rglob('*') if path.is_file()} != expected | {'export.json'}:
            raise ValueError('export contents differ')
        for entry in listing['files']:
            path = root / entry['name']
            if path.is_symlink():
                raise ValueError('export link')
            abrupt.bound(entry, entry['name'], path.read_bytes())
        class ExportCases(ProducedBytesCases, unittest.TestCase):
            export_directory = root
        suite = unittest.defaultTestLoader.loadTestsFromTestCase(ExportCases)
        if suite.countTestCases() == 0:
            raise ValueError('empty produced suite')
        result = unittest.TextTestRunner(stream=sys.stderr).run(suite)
        if not result.wasSuccessful() or result.skipped:
            return 1
        print('b10_abrupt_produced_bytes_checked')
        return 0
    except (ValueError, OSError, KeyError, TypeError) as error:
        print('b10_abrupt_wire_export_invalid:' + type(error).__name__, file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))

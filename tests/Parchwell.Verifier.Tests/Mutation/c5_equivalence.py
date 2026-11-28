#!/usr/bin/env python3
"""Check from Stryker reports that excluded tests lose no kill.

`prove` fails when a mutant is killed only by excluded tests. `compare` lists
mutants that one campaign killed and another did not detect. The rules are in
docs/operations/verifier-mutation.md, "Issue 602 fast-suite equivalence check".
"""
import argparse
import json
from pathlib import Path
import sys

REPORT_NAME = 'mutation-report.json'
SEGMENT = '/repository/'
SCHEMA = 1


class Failure(Exception):
    def __init__(self, name, detail):
        super().__init__(detail)
        self.name = name
        self.detail = detail


def invalid(path, detail):
    return Failure('invalid_report', f'{path}: {detail}')


def read_excluded(path):
    try:
        value = json.loads(Path(path).read_text(encoding='utf-8'))
    except (OSError, ValueError) as error:
        raise Failure('invalid_excluded', f'{path}: {error}')
    if not isinstance(value, list) or not all(isinstance(item, str) for item in value):
        raise Failure('invalid_excluded', f'{path}: not an array of strings')
    for item in value:
        if not item:
            raise Failure('invalid_excluded', f'{path}: empty entry')
        if '(' in item:
            raise Failure('invalid_excluded', f'{path}: entry has a parameter list: {item}')
    if len(set(value)) != len(value):
        raise Failure('invalid_excluded', f'{path}: duplicate entry')
    if value != sorted(value):
        raise Failure('invalid_excluded', f'{path}: entries are not sorted')
    return value


def method_key(name):
    return name.split('(', 1)[0]


def normalize(key, path):
    index = key.find(SEGMENT)
    if index < 0 or index + len(SEGMENT) >= len(key):
        raise invalid(path, f'file key has no {SEGMENT} segment: {key}')
    return key[index + len(SEGMENT):]


def point(value, path):
    if (not isinstance(value, dict) or not isinstance(value.get('line'), int)
            or not isinstance(value.get('column'), int)):
        raise invalid(path, 'location is not made of line and column numbers')
    return value['line'], value['column']


def load_report(report, path):
    """Returns (rows, test names). A row is a dict of one mutant."""
    try:
        data = json.loads(report.read_text(encoding='utf-8'))
    except (OSError, ValueError) as error:
        raise invalid(path, str(error))
    if (not isinstance(data, dict) or not isinstance(data.get('files'), dict)
            or not isinstance(data.get('testFiles'), dict)):
        raise invalid(path, 'not an object with object files and object testFiles')
    names = {}
    for entry in data['testFiles'].values():
        tests = entry.get('tests') if isinstance(entry, dict) else None
        if not isinstance(tests, list):
            raise invalid(path, 'a testFiles entry has no tests list')
        for test in tests:
            if (not isinstance(test, dict) or not isinstance(test.get('id'), str)
                    or not isinstance(test.get('name'), str)):
                raise invalid(path, 'a test has no string id and name')
            if names.setdefault(test['id'], test['name']) != test['name']:
                raise invalid(path, f'test id {test["id"]} has two names')
    rows = []
    for key, entry in data['files'].items():
        file = normalize(key, path)
        mutants = entry.get('mutants') if isinstance(entry, dict) else None
        if not isinstance(mutants, list):
            raise invalid(path, f'file {key} has no mutants list')
        for mutant in mutants:
            rows.append(mutant_row(mutant, file, names, path))
    return rows, names


def mutant_row(mutant, file, names, path):
    if not isinstance(mutant, dict):
        raise invalid(path, 'a mutant is not an object')
    for field in ('id', 'mutatorName', 'replacement', 'location', 'status', 'static'):
        if field not in mutant:
            raise invalid(path, f'a mutant has no {field}')
    location = mutant['location']
    if not isinstance(location, dict):
        raise invalid(path, 'location is not an object')
    start = point(location.get('start'), path)
    end = point(location.get('end'), path)
    if (not isinstance(mutant['mutatorName'], str) or not isinstance(mutant['status'], str)
            or not isinstance(mutant['replacement'], str) or not isinstance(mutant['static'], bool)):
        raise invalid(path, 'a mutant field has the wrong type')
    killers = frozenset()
    if mutant['status'] == 'Killed':
        killed_by = mutant.get('killedBy')
        if not isinstance(killed_by, list) or not killed_by:
            raise Failure('killed_without_tests', f'{path}: mutant {mutant["id"]} is Killed with no killedBy')
        missing = [str(test) for test in killed_by if not isinstance(test, str) or test not in names]
        if missing:
            raise Failure('unmapped_test_id', f'{path}: mutant {mutant["id"]} names unknown test ids {missing}')
        killers = frozenset(method_key(names[test]) for test in killed_by)
    return {
        'identity': (file, mutant['mutatorName'], mutant['replacement'], start, end, mutant['static']),
        'id': str(mutant['id']),
        'status': mutant['status'],
        'location': location,
        'killers': killers,
    }


def load_reports(directory):
    """Returns (rows, report count, every test method key seen)."""
    root = Path(directory)
    found = sorted(path for path in root.rglob(REPORT_NAME) if path.is_file()) if root.is_dir() else []
    if not found:
        raise Failure('no_reports', f'no {REPORT_NAME} under {directory}')
    rows, seen = [], set()
    for report in found:
        report_rows, names = load_report(report, str(report))
        rows.extend(report_rows)
        seen.update(method_key(name) for name in names.values())
    return rows, len(found), seen


def group(rows):
    identities = {}
    for row in rows:
        identities.setdefault(row['identity'], []).append(row)
    return identities


def order(identity):
    file, mutator, replacement, start, end, static = identity
    return file, start, mutator, replacement, end, static


def entry(identity, rows):
    file, mutator, replacement, _, _, static = identity
    return {
        'file': file,
        'mutant_ids': sorted({row['id'] for row in rows}),
        'mutator': mutator,
        'replacement': replacement,
        'location': rows[0]['location'],
        'static': static,
    }


def prove(args):
    excluded = read_excluded(args.excluded)
    rows, count, seen = load_reports(args.reports_dir)
    unmatched = [key for key in excluded if key not in seen]
    if unmatched:
        raise Failure('unmatched_excluded', 'no test in any report is named ' + ', '.join(unmatched))
    banned = set(excluded)
    lost, timeouts, killed = [], [], 0
    for identity, group_rows in sorted(group(rows).items(), key=lambda item: order(item[0])):
        kills = [row for row in group_rows if row['status'] == 'Killed']
        if kills:
            killed += 1
            lost_rows = [row for row in kills if row['killers'] <= banned]
            if lost_rows:
                item = entry(identity, lost_rows)
                item['killed_by'] = sorted(set().union(*(row['killers'] for row in lost_rows)))
                lost.append(item)
        else:
            timed_out = [row for row in group_rows if row['status'] == 'Timeout']
            if timed_out:
                timeouts.append(entry(identity, timed_out))
    return {
        'schema': SCHEMA,
        'status': 'not_equivalent' if lost else 'equivalent',
        'reports': count,
        'killed': killed,
        'excluded': len(excluded),
        'lost': lost,
        'timeouts': timeouts,
    }, 1 if lost else 0


def compare(args):
    excluded = read_excluded(args.excluded)
    before_rows, before_count, _ = load_reports(args.before)
    after_rows, after_count, _ = load_reports(args.after)
    before, after = group(before_rows), group(after_rows)
    banned = set(excluded)
    regressions = []
    for identity in sorted(before, key=order):
        kills = [row for row in before[identity] if row['status'] == 'Killed']
        if not kills:
            continue
        after_group = after.get(identity)
        if after_group is None:
            raise Failure('identity_missing', f'no row in --after for {identity[0]} {identity[1]} at {identity[3]}')
        statuses = {row['status'] for row in after_group}
        if statuses & {'Killed', 'Timeout'}:
            continue
        if not statuses & {'Survived', 'NoCoverage'}:
            raise Failure('identity_unscored', f'{identity[0]} {identity[1]} at {identity[3]} has only {sorted(statuses)}')
        item = entry(identity, kills)
        killers = sorted(set().union(*(row['killers'] for row in kills)))
        item['before_killed_by'] = killers
        item['after_statuses'] = sorted(statuses)
        item['excluded_killers'] = [key for key in killers if key in banned]
        regressions.append(item)
    return {
        'schema': SCHEMA,
        'status': 'regressions' if regressions else 'no_regressions',
        'before_reports': before_count,
        'after_reports': after_count,
        'regressions': regressions,
    }, 1 if regressions else 0


def parser():
    top = argparse.ArgumentParser(description=__doc__)
    commands = top.add_subparsers(dest='command', required=True)
    proving = commands.add_parser('prove')
    proving.add_argument('--reports-dir', required=True)
    proving.add_argument('--excluded', required=True)
    proving.add_argument('--output', required=True)
    comparing = commands.add_parser('compare')
    comparing.add_argument('--before', required=True)
    comparing.add_argument('--after', required=True)
    comparing.add_argument('--excluded', required=True)
    comparing.add_argument('--output', required=True)
    return top


def main(argv=None):
    args = parser().parse_args(argv)
    try:
        result, status = (prove if args.command == 'prove' else compare)(args)
    except Failure as failure:
        print(f'c5_equivalence: {failure.name}: {failure.detail}', file=sys.stderr)
        try:
            Path(args.output).unlink(missing_ok=True)
        except OSError:
            pass
        return 2
    try:
        Path(args.output).write_text(json.dumps(result, sort_keys=True) + '\n', encoding='utf-8')
    except OSError as error:
        print(f'c5_equivalence: cannot write {args.output}: {error}', file=sys.stderr)
        return 2
    return status


if __name__ == '__main__':
    sys.exit(main())

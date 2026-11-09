"""Recompute the issue 412 plan-check classification tables from plan-check-findings.tsv.

Usage: python3 docs/operations/evidence/issue-412/summarize.py
"""
import collections
import csv
from pathlib import Path

FIRST_CHECKS = {
    '6705643741', '6710268101', '6721297894', '6724881731',
    '6726202082', '6726863708', '6734086045',
}
KINDS = ['D1', 'D2', 'L', 'G', 'S']


def gate(value):
    return 'caught anyway' if value.startswith('yes') else value


def main():
    path = Path(__file__).with_name('plan-check-findings.tsv')
    rows = list(csv.DictReader(path.open(encoding='ascii'), delimiter='\t'))
    print(f'findings: {len(rows)}')
    table = collections.Counter((row['class'], gate(row['later_gate'])) for row in rows)
    for kind in KINDS:
        cells = {key[1]: count for key, count in table.items() if key[0] == kind}
        print(kind, sum(cells.values()), dict(sorted(cells.items())))
    executed = [row for row in rows if row['found_by'] == 'exec']
    print(f"needed execution: {len(executed)}; would have slipped: {sum(row['later_gate'] == 'no' for row in executed)}")
    for label, selected in (('first checks', True), ('revision checks', False)):
        part = [row for row in rows if (row['check_id'] in FIRST_CHECKS) == selected]
        slipped = sum(row['later_gate'] == 'no' and row['class'] in ('D1', 'D2') for row in part)
        print(f'{label}: {len(part)} findings, {slipped} slip-through behaviour, build or test defects')


if __name__ == '__main__':
    main()

# B10 restart design receipt

This receipt records the design-only implementation of the approved restart redesign. It contains no scientific run evidence or measured runtime claim.

## Execution card

- Deliverable: PR1 exact design text in the plan's replacement map, plus the item state and this receipt. PR2 and PR3 remain unapproved.
- Authorization: [Revision 5](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6705594392), pinned `updated_at: 2026-11-07T14:07:00Z`, and [PR1 approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6708991894). Apply [P2-1 through P2-7](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6708730209) exactly, as [Hari decided](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6708982964).
- Source commit: `17f42aed5be3794e7aa2fa54e521d02b8ab7a92a`, current main and the plan baseline at the initial refresh.
- Mandatory gates: exact OLD occurrence and line checks before editing; exact FINAL rendering and sentence preservation afterward; documentation checks and warning dispositions; ASCII punctuation, local cross-references, changed-path allowlist; cold review; hosted Codex review and final-head CI. Hari merges the protected paths.
- Commands: the Python script below; `npm ci --ignore-scripts`; `npm run check:docs`; `npm run test:docs`; `git diff --check`. Documentation-check output is retained outside the repository and summarized here.
- Earliest publication checkpoint: the approved text passes the checks and applicable cold review. Hosted review and CI may run together after publication. No merge by this session.
- Budgets: the plan's documentation-warning, verification-failure and review-finding root-cause caps and exhaustion actions apply. Rule 8 permits at most two comprehensive passes and then at most two focused rounds across sessions, including its P2 exception. When another required review would exceed the budget or focus is unsafe, save unpublished work to the branch's `-wip` backup and hand off to Hari. No session resets these counters.
- Predicate-to-test map: not applicable; no verifier code or tests change.

## Authority and scope

The issue body and recognized decisions are indexed in [state.md](state.md). All listed decisions, the approval, the current plan check, AGENTS.md and docs/agents/pull-requests.md were read before implementation. The timestamp was verified by REST before reading the map. The session's direct instruction authorizes Codex for PR1; the issue also names Codex in comment 6705495747. That comment was created after the earlier label event, so the label alone is not used as plan approval.

The complete replacement map is the specification. The script below derives every edit from its literal FINAL text, except the seven exact adopted fixes. Other README rows, the frozen inventory row, ordinary ADR rules and unrelated files remain unchanged. No live B10 run result, artifact or log is an input.

## Specification comparison and verification

Every map entry is applied literally, with P2-1 through P2-7 as adopted. P2-2 preserves historical option 6 and selects the added B10 options in each Decision paragraph. Same-paragraph appends follow all original sentences, in map order. The inventory pointer follows the complete containing table. P2-7 trims the preceding space on terminal DELETEs.

The script checks the plan hash and timestamp, every OLD quote's single occurrence and stated baseline line, exact regenerated files, each original body sentence span, the allowlist and added ASCII. Sentence spans split original body lines at sentence punctuation followed by a capital, backtick, quote or parenthesis; Markdown list and bold paragraph labels are excluded. This preserves literal text rather than normalizing it. The ADR-021 missing set must equal exactly the spans inside its authorized REPLACE/DELETE quotes; historical option 6 is excluded from that set by P2-2. The initial pre-edit script needed a correction to strip the bold Decision label before comparing a replaced sentence; it failed before writing design files. The corrected script passed before edits were written and afterward against the actual files.

Commands and results on the baseline above with the authored PR1 edits:

- `python /tmp/404-map.py --apply`: passed the OLD preflight before writing the design files.
- `python /tmp/404-map.py`: passed against the written files; exact output below is included at the user's explicit request.
- `npm ci --ignore-scripts`: exit 0. npm reported the locked glob deprecation and an npm update notice; no dependency file changed.
- `npm run check:docs`: exit 0 with 8 advisory warnings after the state file was added. The initial scan also warned that this receipt's state-file link had no target yet; creating the authorized state file resolved it. In the changed factory file, the two existing undefined-identifier warnings at baseline lines 7 and 150 refer to the adopted scenario label `S0`; those sentences are outside the exact approved replacement map and remain unchanged. Other warnings are in unchanged files and are left under the rule 8 blanket disposition. No added design text warning was reported.
- `npm run test:docs`: exit 0, 28 tests passed and 0 failed.
- `git diff --check`: exit 0.

The numerical protocol limits are approved policy, not measured capabilities. A source-verification reader checked the cited .NET 10 Start, WaitForExit, DllImport and ExitCode pages, librdkafka v2.16.0 statistics, POSIX.1-2024 sigaction/read, Linux cgroup-v2 memory counters and Kubernetes pod lifecycle. The author checked each reported quotation against the cited source. These sources support the limited API, sampling, signal, EOF, group-counter and deletion claims. Runtime interoperability, cgroup availability, timing, kill attribution and the diagnostic trial remain unverified dependencies of later work. Sampled-wire-state and lost-callback limitations are explicitly labelled inference. The source URLs are preserved in ADR-021 protocol rules 2, 5, 6, 8, 9 and 10.

## Cold-review input

Mode: comprehensive. Input revision: the first local PR1 commit on `codex/404-pr1-design`; comparison revision: `17f42aed5be3794e7aa2fa54e521d02b8ab7a92a`. The reviewer receives only the frozen full authored diff and this receipt. The complete report, exact input commit/hash, dispositions and cumulative review counts belong in the PR description, avoiding a report appended to its own reviewed receipt. All authored files are included. No cold review has been performed at the time this input is frozen. No prior PR1 implementation or cold-review record was present on main or in the issue catch-up.

## Reproducible map and preservation script

Save this code outside the repository as `/tmp/404-map.py` and run it from the repository root. Its default mode verifies the current design files; `--apply` first checks all OLD text against the pinned main and then writes only the three design files. It requires the pinned main reference and unchanged approved plan. The receipt and state are the only additional allowed paths.

```python
import collections
import hashlib
import json
import pathlib
import re
import subprocess
import sys

BASE = '17f42aed5be3794e7aa2fa54e521d02b8ab7a92a'
REPO = 'repos/collaborationwithothers/eventhubs-kafka-dotnet'
STAMP = '2026-11-07T14:07:00Z'
FILES = ['docs/blueprint.md', 'docs/agents/factory.md', 'README.md']

def git(*args):
    return subprocess.check_output(['git', *args], text=True)

def api(route):
    return json.loads(subprocess.check_output(['gh', 'api', REPO + route]))

plan = api('/issues/comments/6705594392')
assert plan['updated_at'] == STAMP, 'STOP: hand off to Hari'
p = plan['body']
assert hashlib.sha256(p.encode()).hexdigest() == '4870cbb1ba434e3eb0f12a83636a152d819f78d3152ab2b1ef46c73812acb63c'
check = api('/issues/comments/6708730209')['body']
assert git('rev-parse', 'origin/main').strip() == BASE
old = {f: git('show', BASE + ':' + f) for f in FILES}
lines = p.splitlines()
edits = []
for i, line in enumerate(lines):
    m = re.fullmatch(r'\*\*(ADR-\d+) (\w+) L(\d+) OLD:\*\*', line)
    if not m:
        continue
    quote = lines[i + 1].removeprefix('> ')
    j = i + 2
    while not lines[j].startswith('**'):
        j += 1
    action = lines[j]
    final = '' if action.startswith('**DELETE') else lines[j + 1]
    if m[1] == 'ADR-021' and m[2] == 'Options' and m[3] == '636':
        action, final = 'KEEP', ''  # P2-2 keeps historical option 6.
    edits.append(dict(adr=m[1], section=m[2], line=int(m[3]), old=quote,
                      action=action, final=final))

# Every P2 replacement is copied from the adopted check, not paraphrased.
p2 = {
    'For B10, the ADR-012 and ADR-013 source category includes only the explicit ADR-021 exceptions; the closed list still has four source categories.':
    'For B10, that source category is the ADR-012 and ADR-013 invalid-run conditions as modified only by the explicit ADR-021 exceptions; the closed list still has four source categories.',
    "Option 4's late-record classification remains. Option 6 is amended for C1 by the checked termination protocol and separate restart verdict below, under the [restart redesign decisions](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6705801800). The [earlier clean-restart decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6623554949) remains historical context.":
    "Option 4's late-record classification remains. For C1, option 9 replaces option 6, through the checked termination protocol and separate restart verdict below, under the [restart redesign decisions](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6705801800). The [earlier clean-restart decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6623554949) remains historical context.",
    'It may not launch another CLI tool, invoke a shell,':
    'It may not start any CLI tool or other process, invoke a shell,',
    'The last statistics report to kill interval and the killed native/callback tail remain unobserved. Missing evidence outside the enumerated exception still invalidates eligibility.':
    "For B10's kill probe, the interval from the last statistics report to the kill and the killed native/callback tail remain unobserved. Missing evidence outside ADR-021's enumerated exception still invalidates eligibility.",
    'Here and in PR2, "authenticated" means':
    'In ADR-021, in the B10 exceptions of ADR-012, ADR-013, ADR-019 and ADR-020, and in PR2, "authenticated" means',
    'Failure preserves diagnostics and stops scientific activation for investigation, with no automatic second trial.':
    'Policy cap: one trial. On failure or exhaustion, record the gap and continue to diagnostics and cleanup only, with no scientific activation and no automatic second trial; a second trial spends money, so the session hands off to Hari.'
}
for replacement in p2.values():
    assert replacement in check, replacement

def fixed(text):
    for a, b in p2.items():
        text = text.replace(a, b)
    return text

def verify_old(file, number, quote):
    assert old[file].count(quote) == 1, (file, number, 'occurrence', quote)
    assert quote in old[file].splitlines()[number - 1], (file, number, quote)

for e in edits:
    verify_old(FILES[0], e['line'], e['old'])

def paragraph_after(label):
    return p.split(label, 1)[1].lstrip('\n').split('\n\n', 1)[0]

bp_extra = [
    (8, 'ADR-021, and the references to it in ADR-012, ADR-013 and ADR-020, follow'),
    (4, 'Anything else found later goes to v1.1.'),
    (1128, '| B04, B10 | Idempotence does not survive a pod restart (K-53) |')
]
for number, start in bp_extra:
    quote = next(x for x in lines if x.startswith(start))
    verify_old(FILES[0], number, quote)

factory_section = p.split('### Factory Evidence pack and Item lifecycle\n', 1)[1].split('### README', 1)[0]
factory_quotes = []
for match in re.finditer(r'`docs/agents/factory.md`, baseline line (\d+)\. Old \(verbatim\):\n\n([^\n]+)', factory_section):
    number, quote = int(match[1]), match[2]
    verify_old(FILES[1], number, quote)
    tail = factory_section[match.end():]
    final = tail.split('FINAL sentences', 1)[1].split('\n\n', 1)[1].split('\n\n', 1)[0]
    factory_quotes.append((quote, final))

readme_section = p.split('### README restart cell and Verdicts line\n', 1)[1].split('## Three ordered', 1)[0]
readme_quotes = []
for match in re.finditer(r'`README.md`, baseline line (\d+)\. Old \(verbatim\):\n\n([^\n]+)\n\nFINAL replacement:\n\n([^\n]+)', readme_section):
    verify_old(FILES[2], int(match[1]), match[2])
    readme_quotes.append((match[2], match[3]))
count = len(edits) + len(bp_extra) + len(factory_quotes) + len(readme_quotes)
print('Baseline:', BASE)
print('Plan updated_at:', STAMP)
print('OLD quotes checked once at stated lines:', count,
      '(ADR:', len(edits), 'blueprint positions:', len(bp_extra),
      'factory:', len(factory_quotes), 'README:', len(readme_quotes), ')')

new = dict(old)
# Group appends by ORIGINAL paragraph so each follows every old sentence.
bp_lines = old[FILES[0]].splitlines()
grouped = collections.defaultdict(list)
table_bullets = []
for e in edits:
    grouped[e['line']].append(e)
for number, group in grouped.items():
    text = bp_lines[number - 1]
    appends = []
    for e in group:
        a, b = e['old'], fixed(e['final'])
        if e['action'].startswith('**REPLACE'):
            text = text.replace(a, b)
        elif e['action'].startswith('**DELETE'):
            text = text.replace(a, '')
        elif b:
            if 'after the complete four-class table' in e['action']:
                table_bullets.append('- ' + b)
            elif 'indent four spaces' in e['action']:
                appends.append('\n\n    ' + b)
            else:
                appends.append(' ' + b)
    bp_lines[number - 1] = text.rstrip() + ''.join(appends)  # P2-7.
new[FILES[0]] = '\n'.join(bp_lines) + '\n'

def replace_once(text, a, b):
    assert text.count(a) == 1, (a, text.count(a))
    return text.replace(a, b)

bp = new[FILES[0]]
for adr in ['012', '013', '019', '020', '021']:
    label = f'### ADR-{adr} amendment, docs/blueprint.md section 4\n'
    section = p.split(label, 1)[1].split('\n### ', 1)[0]
    context = section.split('**Context FINAL: new paragraph before Options considered:**\n', 1)[1].split('\n', 1)[0]
    opt = re.search(r'\*\*Options FINAL: new numbered option (\d+) before Decision; prefix "[^\n]+\n([^\n]+)', section)
    consequence = re.search(r'\*\*Consequences FINAL:[^\n]+\n([^\n]+)', section)[1]
    start = bp.index('### ADR-' + adr + ':')
    end_match = re.search(r'\n(?:### ADR-|---)', bp[start:])
    end = start + end_match.start()
    block = bp[start:end]
    block = replace_once(block, '**Options considered.**', context + '\n\n**Options considered.**')
    decision = re.search(r'\*\*Decision\.\*\*[^\n]+', block)[0]
    block = replace_once(block, decision, opt[1] + '. ' + opt[2] + '\n\n' + decision)
    if adr != '021':
        option = '5' if adr in ['012', '019'] else '4'
        selection = f'For B10 only, option {option} also applies, through the B10-only exceptions below and ADR-021.'
        assert selection in check if option == '5' else 'the same text with `option 4`' in check
        block = replace_once(block, decision, decision + ' ' + selection)
    if adr == '012':
        last_table_row = '| reported failure | failed | no |'
        block = replace_once(block, last_table_row, last_table_row + '\n\n' + '\n'.join(table_bullets))
    if adr == '021':
        rules = p.split('\n- Restart decision rules.\n', 1)[1].split('\n**Consequences FINAL:', 1)[0]
        block = replace_once(block, '**Consequences.**', '- Restart decision rules.\n' + fixed(rules).rstrip() + '\n\n**Consequences.**')
    block = block.rstrip() + '\n- ' + fixed(consequence) + '\n'
    bp = bp[:start] + block + bp[end:]

for number, start in bp_extra[:2]:
    quote = next(x for x in lines if x.startswith(start))
    # First append instruction belongs to provenance; second to inventory log.
    label = 'APPEND this FINAL paragraph immediately after the old sentence:'
    pos = p.index(quote)
    final = p[p.index(label, pos) + len(label):].lstrip('\n').split('\n\n', 1)[0]
    if number == 4:
        # OLD is the last sentence of the complete opening paragraph.
        bp = replace_once(bp, quote, quote + '\n\n' + final)
    else:
        bp = replace_once(bp, quote, quote + '\n\n' + final)
row = bp_extra[2][1]
start = bp.index(row)
end = bp.index('\n\n', start)
pointer = paragraph_after('APPEND this FINAL paragraph after the complete containing table (not inside its rows):')
bp = bp[:end] + '\n\n' + pointer + bp[end:]
new[FILES[0]] = bp
for quote, final in factory_quotes:
    new[FILES[1]] = replace_once(new[FILES[1]], quote, quote + ' ' + final)
pack = factory_section.split('insert this FINAL paragraph:\n\n', 1)[1].strip()
new[FILES[1]] = replace_once(new[FILES[1]], '\nFor a traced item,', '\n' + pack + '\n\nFor a traced item,')
for quote, final in readme_quotes:
    new[FILES[2]] = replace_once(new[FILES[2]], quote, final)

def block(text, adr):
    return text.split('### ADR-' + adr + ':', 1)[1].split('\n### ADR-', 1)[0].split('\n---', 1)[0]

def sentences(text):
    result = []
    for line in text.splitlines()[1:]:  # Skip heading text, keep all body prose.
        line = re.sub(r'^\s*(?:- |\d+\. )', '', line).strip()
        line = re.sub(r'^\*\*[^*]+\.\*\*\s*', '', line)
        if line:
            result += re.split(r'(?<=[.!?])\s+(?=[A-Z`"(])', line)
    return result

for adr in ['012', '013', '019', '020', '021']:
    before, after = block(old[FILES[0]], adr), block(new[FILES[0]], adr)
    spans = sentences(before)
    missing = [s for s in spans if s not in after]
    allowed = [e['old'] for e in edits if e['adr'] == 'ADR-' + adr
               and e['action'].startswith(('**REPLACE', '**DELETE'))]
    unexpected = [s for s in missing if not any(s in a for a in allowed)]
    assert not unexpected, (adr, unexpected)
    if adr != '021':
        assert not missing
    else:
        expected = [s for s in spans if any(s in a for a in allowed)]
        assert collections.Counter(missing) == collections.Counter(expected)
    print('ADR-' + adr + ':', len(spans), 'original sentence spans;',
          len(missing), 'missing;', len(unexpected), 'unmapped losses')

if '--apply' in sys.argv:
    for f in FILES:
        pathlib.Path(f).write_text(new[f])
else:
    for f in FILES:
        assert pathlib.Path(f).read_text() == new[f], (f, 'differs from exact map')
print('Exact FINAL text, KEEP/APPEND positions and P2-1 through P2-7: PASS')
allowed_paths = set(FILES + ['items/404/state.md', 'items/404/pr1-receipt.md'])
changed = set(git('diff', '--name-only', BASE).splitlines())
untracked = set(git('ls-files', '--others', '--exclude-standard').splitlines())
assert changed | untracked <= allowed_paths, changed | untracked
for f in changed | untracked:
    added = git('diff', '--no-ext-diff', '--unified=0', BASE, '--', f)
    if f in untracked:
        added = '+' + pathlib.Path(f).read_text().replace('\n', '\n+')
    assert all(ord(c) < 128 for line in added.splitlines()
               if line.startswith('+') and not line.startswith('+++') for c in line), f
assert not subprocess.run(['git', 'diff', '--check']).returncode
print('Changed-path allowlist, added ASCII and git diff --check: PASS')
```

Script output:

```text
Baseline: 17f42aed5be3794e7aa2fa54e521d02b8ab7a92a
Plan updated_at: 2026-11-07T14:07:00Z
OLD quotes checked once at stated lines: 62 (ADR: 55 blueprint positions: 3 factory: 2 README: 2 )
ADR-012: 88 original sentence spans; 0 missing; 0 unmapped losses
ADR-013: 93 original sentence spans; 0 missing; 0 unmapped losses
ADR-019: 78 original sentence spans; 0 missing; 0 unmapped losses
ADR-020: 73 original sentence spans; 0 missing; 0 unmapped losses
ADR-021: 87 original sentence spans; 18 missing; 0 unmapped losses
Exact FINAL text, KEEP/APPEND positions and P2-1 through P2-7: PASS
Changed-path allowlist, added ASCII and git diff --check: PASS
```

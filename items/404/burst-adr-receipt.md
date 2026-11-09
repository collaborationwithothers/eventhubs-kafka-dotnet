# B10 burst ADR amendment receipt

This pull request applies only the approved ADR-021 burst amendment. It changes design policy; burst execution needs a separate approval after this pull request merges.

## Execution card

- Deliverable: PR-A under [Revision 5.1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6741960293), applying [appendix A1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6741953375) exactly to `docs/blueprint.md`; this receipt and `state.md` satisfy factory.md "Item state file" and pull-requests.md rule 8.
- Authorization: [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6741989166), PR-A only. Implementing tool: Codex. The interactive task also limits work to PR-A and reserves merging to Hari.
- Mandatory gates: verified current approval and both approved bodies; exact A1 mapping with no baseline drift; documentation check and warning dispositions; rule-8 cold review; hosted Codex review of the head. Hari merges the protected blueprint. No workflow dispatch, results or expected-outcome change.
- Verification: `python3 /tmp/404-burst-amendment.py`; `npm run check:docs`; `git diff --check`. The amendment checker verifies both approved hashes/timestamps, all old text, the exact resulting blueprint and the changed-path allowlist. Its reproducible source is below.
- Publication checkpoint: first clean applicable cold review after the documentation check and exact-map verification. CI and hosted review may proceed together after publication.
- Budget: PR-A starts a new PR budget, comprehensive 0/2 and focused 0/2 used at freeze. Apply pull-requests.md rule 8, including its handoff to Hari on exhaustion or unsafe focus. The plan's "Caps and exhaustion actions" caps documentation and review dispositions at 20 root causes each; on that exhaustion, push `-wip` and hand off as stated there. Earlier diagnostics PR budgets remain recorded in their own receipts and are not reset.
- Predicate map: not applicable; no verifier or executable predicate changes.

## Authority and source comparison

Base: `4163cd1afcfb4a3564c80f0eda9aff76cb0a9fe0`, also the plan check's baseline. No later decision alters A1 at initial catch-up. The [current plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6741980175) records no blocking findings and no apply-in-implementation amendment to A1.

Verified exact-body UTF-8 hashes, with no added newline or normalization:

| Source | updated_at | SHA-256 |
|---|---|---|
| Plan | 2026-11-09T14:03:09Z | `54d27d8b010e45ceff9d649e2bcd6f810daf7026d967a5587ce47cd00cb07f98` |
| Appendix | 2026-11-09T14:02:50Z | `57945a00f987b335a8e198721cb73915c28505458fe5f7408e3f0dd28587299b` |

The new factual observation in protocol rule 15 is from synthetic diagnostic run [42608381540](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42608381540), artifact 13668809132. The downloaded ZIP matches the plan's SHA-256 `0c22924e47ed6b455e228f98be5016c6e3c97faab541d9ed2f9853bf1005eb30`. In `b10-42608381540-1-c1-r1-loadgen-zdc8s.containers.loadgen.log`, the `failed_diagnostic` record is bound to the base commit and reports `CheckpointControlAsync`, accepted_count 1, selected_count 0, frames_drained_after_stop 1 and delivered 1. This verifies the single original was confirmed before the completed checkpoint; no rate or general burst effectiveness is claimed. The burst size is approved policy, not a measurement. A1's selected-member interpretation is policy for the later verifier work.

No new Azure, Kafka or librdkafka capability is asserted. Existing protocol citations and every paragraph outside the six amended rules are preserved. The PR-A gate does not require PR-B's runtime acceptance commands; runtime behavior remains for PR-B.

## Verification and review inputs

The exact-map check passes: eight replacements and one append affect only protocol rules 1, 5, 6, 7, 8 and 15. Source commit is the base above; the checked candidate is that base plus the authored PR diff. The synthetic trial ZIP hash and checkpoint facts also pass the source check. `git diff --check` passes. `npm ci --ignore-scripts` completed with the locked dependencies. `npm run check:docs` completed successfully with eight advisory warnings, all in unchanged files; every changed file is clean. Those existing warnings stand as unrelated to this amendment. The final pre-review scan is repeated after this receipt update. C1 max.in.flight 5 is also pinned in `scenarios/B10/scenario.yaml` at the base commit.

The cold reviewer applies `.agents/skills/repo-review/SKILL.md` and returns a complete report without editing the tree. Review mode: comprehensive pass 1, comparison revision the base above, input revision the frozen candidate commit identified in the PR self-review record. All authored changes, including this receipt and the item state, are in scope. Complete reports and dispositions belong in the PR self-review record rather than being appended to their own reviewed input.

The earlier diagnostics process decision also names an exact-head Claude review. This environment supplies no Claude executable or session tool. The control session must provide that review before merge; the local fresh-context rule-8 review and hosted Codex reviews do not claim to satisfy it.

## Reproducible amendment check

Save this authored code as `/tmp/404-burst-amendment.py` outside the repository. Its normal mode verifies; `--apply` requires an unchanged baseline before writing. It stops on approval or blueprint drift.

```python
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

BASE = '4163cd1afcfb4a3564c80f0eda9aff76cb0a9fe0'
REPO = 'repos/collaborationwithothers/eventhubs-kafka-dotnet'
PINS = {
    6741960293: ('2026-11-09T14:03:09Z', '54d27d8b010e45ceff9d649e2bcd6f810daf7026d967a5587ce47cd00cb07f98'),
    6741953375: ('2026-11-09T14:02:50Z', '57945a00f987b335a8e198721cb73915c28505458fe5f7408e3f0dd28587299b'),
}

def git(*args):
    return subprocess.check_output(['git', *args], text=True)

comments = {}
for cid, (stamp, digest) in PINS.items():
    c = json.loads(subprocess.check_output(['gh', 'api', f'{REPO}/issues/comments/{cid}']))
    assert c['updated_at'] == stamp, 'STOP: timestamp drift'
    assert hashlib.sha256(c['body'].encode()).hexdigest() == digest, 'STOP: body drift'
    comments[cid] = c['body']
appendix = comments[6741953375]
pairs = re.findall(r'Replace "([^"\n]+)" with:?(?:\n   | )"([^"\n]+)"', appendix)
assert len(pairs) == 8
append = appendix.split('Then append:\n   "', 1)[1].split('"\n\nWhy', 1)[0]
assert append.endswith('needs another recorded decision with evidence.')
path = 'docs/blueprint.md'
base = git('show', f'{BASE}:{path}')
assert git('show', f'origin/main:{path}') == base, 'STOP: blueprint baseline drift'
expected = base
for old, new in pairs:
    assert base.count(old) == 1, ('STOP: old quote mismatch', old)
    assert expected.count(old) == 1
    expected = expected.replace(old, new, 1)
old15 = next(line for line in base.splitlines() if line.startswith('  15. PR2 is the inactive smoke-only'))
new15 = old15
for old, new in pairs:
    new15 = new15.replace(old, new)
assert expected.count(new15) == 1
expected = expected.replace(new15, new15 + ' ' + append, 1)
if '--apply' in sys.argv:
    assert Path(path).read_text() == base, 'STOP: local blueprint drift'
    Path(path).write_text(expected)
assert Path(path).read_text() == expected, 'STOP: not exact A1 result'
changed_lines = [(a, b) for a, b in zip(base.splitlines(), expected.splitlines()) if a != b]
assert len(base.splitlines()) == len(expected.splitlines())
assert len(changed_lines) == 6
for old, new in changed_lines:
    assert re.match(r'  (1|5|6|7|8|15)\. ', old)
allowed = {path, 'items/404/state.md', 'items/404/burst-adr-receipt.md'}
changed = set(git('diff', '--name-only', BASE).splitlines())
changed |= set(git('ls-files', '--others', '--exclude-standard').splitlines())
assert changed <= allowed, ('STOP: path scope', changed - allowed)
for file in changed:
    diff = git('diff', '--unified=0', BASE, '--', file)
    if not git('ls-files', '--', file).strip():
        diff = '+' + Path(file).read_text().replace('\n', '\n+')
    assert all(ord(c) < 128 for line in diff.splitlines() if line.startswith('+') and not line.startswith('+++') for c in line)
subprocess.run(['git', 'diff', '--check'], check=True)
print('PASS: both approved hashes/timestamps, 8 replacements plus append, 6 protocol rules, exact blueprint, path scope and ASCII')
```

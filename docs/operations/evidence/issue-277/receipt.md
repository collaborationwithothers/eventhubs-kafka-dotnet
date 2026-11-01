# Issue 277 receipt

This records the update of 11 published evidence packs. Each pack called the 260 configuration-judgement survivors of C5 run 41232286130 "a separate item". Each now links the three disposition sections and states the counts: 252 missing test and 8 equivalent. No raw Stryker status, score, verdict or expected outcome changes.

## Execution card

- Deliverable: the 11 one-line pack edits, this receipt and `items/277/state.md` of the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6560320119), Revision 2.
- Authorization: plan Revision 2 (`updated_at` 2026-11-01T16:10:45Z), checked by [plan check 6560675226](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6560675226) and approved by [comment 6561200897](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6561200897), including the three `apply in implementation` P2s, applied below. Later decisions: [6557335536](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6557335536), [6560241481](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6560241481), [6560392252](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6560392252).
- Mandatory gates: the acceptance commands below, the cold review under AGENTS.md "Before opening a pull request" rule 8, hosted CI, Codex review and Hari's approval and merge. `results/` is protected and GOVERNANCE "Verdict integrity" applies. The pull request is labelled `verdict`.
- Publication checkpoint: open the pull request after acceptance 1 to 7 pass and the first cold review has no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: its review-budget handoff to Hari.

## Predicate map

Predicate map: not applicable, no verifier work.

## Scripts

Both scripts are the plan's text, written to a scratch directory outside the repository (`$SCRATCH`) and run from the repository root, for example `python3 -I "$SCRATCH/update.py"`.

`update.py`:

```python
import pathlib, sys
T = "`VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs`"
I277 = "[issue 277](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277)"
D = "../../docs/operations/verifier-mutation.md#issue-"
LINKS = ("each have one disposition in "
         f"[Issue 281 survivor dispositions]({D}281-survivor-dispositions), "
         f"[Issue 282 survivor dispositions]({D}282-survivor-dispositions) and "
         f"[Issue 283 survivor dispositions]({D}283-survivor-dispositions): "
         "252 missing test and 8 equivalent. "
         "The tests behind the missing-test dispositions merged after run 41232286130, "
         "which did not measure them.")
A_OLD = f"The 260 survivors of that configuration target are a separate item, {I277}."
A_NEW = f"The 260 survivors of that configuration target {LINKS}"
B_OLD = f"The 260 survivors of {T}, the target that judges this item, have no disposition. They are a separate item, {I277}."
B_NEW = f"The 260 survivors of {T}, the target that judges this item, {LINKS}"
C_OLD = f"The 260 survivors of {T}, the target that judges this item, are a separate item, {I277}."
C_NEW = B_NEW
PLAN = {
    "results/A01/2026-10-25.md": "A", "results/B01/2026-10-27.md": "A",
    "results/B03/2026-10-27.md": "A", "results/B04/2026-10-26.md": "A",
    "results/B05/2026-10-27.md": "A", "results/C08/2026-10-28.md": "A",
    "results/C12/2026-10-27.md": "A", "results/F04/2026-10-28.md": "A",
    "results/B08/2026-11-01.md": "B", "results/E01/2026-11-01.md": "B",
    "results/F06/2026-11-01.md": "C",
}
PAIRS = {"A": (A_OLD, A_NEW), "B": (B_OLD, B_NEW), "C": (C_OLD, C_NEW)}
found = sorted(str(p) for p in pathlib.Path("results").rglob("*") if p.is_file() and "issues/277" in p.read_text(encoding="utf-8"))
if found != sorted(PLAN):
    sys.exit(f"STOP: files linking issue 277 differ from the plan: {found}")
for f, v in PLAN.items():
    p = pathlib.Path(f); s = p.read_text(encoding="utf-8"); old, new = PAIRS[v]
    if s.count(old) != 1 or s.count("issues/277") != 1:
        sys.exit(f"STOP: {f} variant {v}: old sentence count {s.count(old)}, issue 277 links {s.count('issues/277')}")
    p.write_text(s.replace(old, new), encoding="utf-8")
    print(f"updated {f} variant {v}")
```

`count.py`:

```python
import re,sys,collections
t=open(sys.argv[1]).read().split('\n')
secs={}
cur=None
for l in t:
    m=re.match(r'^## (.*)',l)
    if m: cur=m.group(1); continue
    if cur and cur.startswith('Issue 28') and l.startswith('|'):
        secs.setdefault(cur,[]).append(l)
tot=collections.Counter()
for s,rows in secs.items():
    hdr=[c.strip() for c in rows[0].strip('|').split('|')]
    di=hdr.index('Disposition'); mi=hdr.index('Mutant')
    c=collections.Counter(); ids=set()
    for r in rows[2:]:
        cs=[x.strip() for x in r.strip('|').split('|')]
        if len(cs)!=len(hdr): continue
        c[cs[di]]+=1; ids.add(cs[mi])
    print(s, hdr, dict(c), 'rows',sum(c.values()),'distinct',len(ids))
    tot+=c
print('total',dict(tot),sum(tot.values()))
```

## Commands and results

Branch cut from `main` at `99131de`. `git diff --stat 831a2e7 origin/main -- results/ docs/operations/verifier-mutation.md` was empty, so the plan's measured inputs had not changed.

1. `python3 -I "$SCRATCH/update.py"` printed 11 lines `updated results/<ID>/<date>.md variant <A|B|C>` in the plan's mapping (8 A, 2 B, 1 C) and exited 0.
2. `git diff --numstat origin/main -- results/` printed 11 lines, each `1	1	<path>`.
3. `grep -rl 'issues/277' results/` printed nothing and exited 1.
4. The two-sentence `grep -rlF` of the plan matched 11 files.
5. `python3 -I "$SCRATCH/count.py" docs/operations/verifier-mutation.md` printed exactly these four lines:

   ```
   Issue 281 survivor dispositions ['Mutant', 'Line', 'Mutator', 'Disposition', 'Test or proof'] {'missing test': 61, 'equivalent': 2} rows 63 distinct 63
   Issue 282 survivor dispositions ['Mutant', 'Line', 'Mutator', 'Disposition', 'Test or proof'] {'missing test': 94} rows 94 distinct 94
   Issue 283 survivor dispositions ['Mutant', 'Line', 'Mutator', 'Disposition', 'Test or proof'] {'missing test': 97, 'equivalent': 6} rows 103 distinct 103
   total {'missing test': 252, 'equivalent': 8} 260
   ```

6. `npm ci --ignore-scripts && npm run check:docs` ended with `⚠ 8 warnings` and `Documentation scan completed. Warnings are advisory; external URLs were not checked.` The 8 are the baseline warnings: docs/agents/factory.md 2, docs/operations/evidence/issue-124/README.md 1, docs/operations/evidence/issue-140/README.md 2 and docs/operations/evidence/issue-211/receipt.md 3. None is in a changed file.
7. `npm run test:docs` printed `# pass 28` and `# fail 0`.
8. `git diff --name-only origin/main` lists the 11 packs, this receipt and `items/277/state.md`, once all are staged.

## Cold review

- Round 1: comprehensive, 1 of at most 2 comprehensive passes; cumulative focused rounds at its start: 0. Input revision: `d24f03aa9ff71a1ae86d1d022d01052d816421a8`. Comparison: `origin/main` at `99131dee48677b971f0b2299d243779151ebe700`.
- Specification sources: the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6560320119), Revision 2, its [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6560675226) P2s as applied, [approval 6561200897](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6561200897), and decisions [6557335536](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6557335536), [6560241481](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6560241481) and [6560392252](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6560392252).
- Round 1 result: one P1 (the state file credited a decision with the child issue numbers it does not name) and two P2s (this section, and the publication checkpoint wording). All three are fixed. Later rounds and the final-head CI run are in the pull request's self-review record.

#!/usr/bin/env python3
"""Inserts or verifies the issue 275 current-gate block in eight evidence packs.

Run from the repository root after git fetch origin main: packs.py report | packs.py apply | packs.py verify
"""
import subprocess, sys

MEASURED = "2dc9715e3de176f7e1cd55eee9c251e43c29c746"
HEADING = "## Mutation publication gate\n\n"
TARGETS = ["Judge/PhaseJudge.cs", "VerifyRun/Publication/PublicationAcceptance.cs", "VerifyRun/A01/A01EvidenceRecorder.cs",
           "VerifyRun/A01/WorkloadRecorder.cs", "VerifyRun/SingleAction/SingleActionRepeatJudgement.cs",
           "VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs"]
REPEAT, CONFIGURATION = "`" + TARGETS[4] + "`", "`" + TARGETS[5] + "`"
BLOCK = """Current gate: [C5 run 41232286130](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41232286130), attempt 1, measured `main` at `2dc9715e3de176f7e1cd55eee9c251e43c29c746`, after the [issue 261 instrumentation repair](../../docs/operations/verifier-mutation.md#issue-261-excluded-source-instrumentation-repair). Its [summary artifact 13057507952](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41232286130/artifacts/13057507952), member `summary.json`, records `measurement_complete: true`, no incomplete reasons and `break_passed: true`, across all 13 shards of the six configured targets. The break threshold is 60, from `thresholds.break` in `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` at the measured commit. The raw score is **91.4%**, from 3076 / 3366 * 100 rounded to one decimal place: 3076 detected out of 3366 scored mutants, 3075 Killed plus 1 Timeout.

| Target | Killed | Timeout | Survived | NoCoverage | CompileError | RuntimeError | Ignored | Detected / scored |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `Judge/PhaseJudge.cs` | 189 | 1 | 16 | 0 | 4 | 0 | 6 | 190 / 206 |
| `VerifyRun/Publication/PublicationAcceptance.cs` | 177 | 0 | 6 | 0 | 6 | 0 | 14 | 177 / 183 |
| `VerifyRun/A01/A01EvidenceRecorder.cs` | 71 | 0 | 0 | 0 | 15 | 0 | 8 | 71 / 71 |
| `VerifyRun/A01/WorkloadRecorder.cs` | 95 | 0 | 0 | 0 | 2 | 0 | 7 | 95 / 95 |
| `VerifyRun/SingleAction/SingleActionRepeatJudgement.cs` | 792 | 0 | 8 | 0 | 142 | 0 | 29 | 792 / 800 |
| `VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs` | 1751 | 0 | 260 | 0 | 158 | 0 | 46 | 1751 / 2011 |
| All six targets | 3075 | 1 | 290 | 0 | 327 | 0 | 110 | 3076 / 3366 |

Of the 290 survivors, the 30 outside `VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs` each have one disposition in [Issue 275 survivor dispositions](../../docs/operations/verifier-mutation.md#issue-275-survivor-dispositions): 25 equivalent and 5 killed on replay. The one Timeout, mutant 4863, has a disposition there too: killed on replay. The 260 survivors of that configuration target are a separate item, [issue 277](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277). The dispositions change no raw status or score.

CHANGE_SENTENCE

The earlier citation below is kept as history. It was measured before the issue 261 repair. Both raw scores count Timeout as detected, as docs/operations/verifier-mutation.md defines the numerator.

"""
LEAD = "Compared with `{0}`, the commit the earlier citation below measured, "
SINGLE = (REPEAT + " and " + CONFIGURATION + " changed before `" + MEASURED + "`. The other four targets are"
          " byte-identical at both commits.", ["same", "same", "same", "same", "changed", "changed"])
SENTENCES = {
    "fd0a65cdf7f72d2aea0e7e1f0cc2d490aa87430a": ("`Judge/PhaseJudge.cs` changed before `" + MEASURED + "`, and the two"
        " single-action targets did not yet exist. The other three targets are byte-identical at both commits.",
        ["changed", "same", "same", "same", "absent", "absent"]),
    "cb83254f1c2645acd86fe734b24e542e0956edd3": ("the four targets it measured are byte-identical at `" + MEASURED + "`."
        " The two single-action targets did not yet exist at that commit.", ["same", "same", "same", "same", "absent", "absent"]),
    "7b6834c5e8668e89376597bc2b524c0725326daf": SINGLE,
    "1f14866faf395ecb28d3b9009986c1b332074386": SINGLE,
}
# Decisions 6551231489 and 6554536413: the sentence follows whenever a configured target's source differs between the
# commit that produced the pack's verdict and MEASURED. A target absent at the verdict commit does not count as differing.
NOT_MEASURED = (" The published verdict ran on `{0}`, and {1} between that commit and `" + MEASURED + "`. Run 41232286130"
                " therefore did not measure the verifier source that produced this pack's published verdict.")
PACKS = {
    "results/A01/2026-10-25.md": "fd0a65cdf7f72d2aea0e7e1f0cc2d490aa87430a",
    "results/B04/2026-10-26.md": "cb83254f1c2645acd86fe734b24e542e0956edd3",
    "results/B01/2026-10-27.md": "7b6834c5e8668e89376597bc2b524c0725326daf",
    "results/B03/2026-10-27.md": "7b6834c5e8668e89376597bc2b524c0725326daf",
    "results/B05/2026-10-27.md": "7b6834c5e8668e89376597bc2b524c0725326daf",
    "results/C12/2026-10-27.md": "7b6834c5e8668e89376597bc2b524c0725326daf",
    "results/C08/2026-10-28.md": "1f14866faf395ecb28d3b9009986c1b332074386",
    "results/F04/2026-10-28.md": "1f14866faf395ecb28d3b9009986c1b332074386",
}
# The final run each pack's "Approved inputs" section names, and that run's head_sha.
VERDICTS = {
    "results/A01/2026-10-25.md": ("40227770436", "fa417d783e833ad222ab5173978433448e4aefbe"),
    "results/B04/2026-10-26.md": ("40315155000", "cb83254f1c2645acd86fe734b24e542e0956edd3"),
    "results/B01/2026-10-27.md": ("40415641139", "7b6834c5e8668e89376597bc2b524c0725326daf"),
    "results/B03/2026-10-27.md": ("40415641139", "7b6834c5e8668e89376597bc2b524c0725326daf"),
    "results/B05/2026-10-27.md": ("40415641139", "7b6834c5e8668e89376597bc2b524c0725326daf"),
    "results/C12/2026-10-27.md": ("40415641139", "7b6834c5e8668e89376597bc2b524c0725326daf"),
    "results/C08/2026-10-28.md": ("40722187672", "20f17794330b60d77a75bf5788ee442cc8baa1f6"),
    "results/F04/2026-10-28.md": ("40722187672", "20f17794330b60d77a75bf5788ee442cc8baa1f6"),
}


def git(*args):
    return subprocess.run(["git", *args], capture_output=True, text=True)


def blob(commit, target):
    result = git("rev-parse", "--verify", "--quiet", f"{commit}:src/Parchwell.Verifier/{target}")
    return result.stdout.strip() if result.returncode == 0 else None


def differing(commit):
    found = [t for t in TARGETS if blob(commit, t) is not None and blob(commit, t) != blob(MEASURED, t)]
    if not found:
        return ""
    names = " and ".join("`" + t + "`" for t in found)
    return NOT_MEASURED.format(commit, names + (" differs" if len(found) == 1 else " differ"))


def expected(path, earlier):
    base = git("show", f"origin/main:{path}").stdout
    if base.count(HEADING) != 1:
        sys.exit(f"{path}: gate heading not found exactly once")
    if earlier not in base.split(HEADING, 1)[1].split("\n## ", 1)[0]:
        sys.exit(f"{path}: earlier commit not cited in the gate section")
    run, verdict = VERDICTS[path]
    inputs = base.split("\n## Approved inputs", 1)[1].split("\n## ", 1)[0] if "\n## Approved inputs" in base else ""
    if f"actions/runs/{run}" not in inputs or f"source `{verdict}`" not in inputs:
        sys.exit(f"{path}: verdict run or source not named in the approved inputs section")
    if git("merge-base", "--is-ancestor", verdict, MEASURED).returncode:
        sys.exit(f"{verdict}: not an ancestor of {MEASURED}")
    tail = differing(verdict)
    text = BLOCK.replace("CHANGE_SENTENCE", LEAD.format(earlier) + SENTENCES[earlier][0] + tail)
    return base.replace(HEADING, HEADING + text, 1)


def verify():
    problems = []
    for earlier, (_, kinds) in SENTENCES.items():
        if git("merge-base", "--is-ancestor", earlier, MEASURED).returncode:
            problems.append(f"{earlier}: not an ancestor of {MEASURED}")
        for target, kind in zip(TARGETS, kinds):
            old, new = blob(earlier, target), blob(MEASURED, target)
            found = "absent" if old is None else ("same" if old == new else "changed")
            if found != kind:
                problems.append(f"{earlier} {target}: {found}, sentence says {kind}")
    for path, earlier in PACKS.items():
        if open(path, encoding="utf-8").read() != expected(path, earlier):
            problems.append(f"{path}: differs from origin/main plus the block")
    print(f"packs {len(PACKS)} problems {len(problems)}")
    print("\n".join(problems))
    return 1 if problems else 0


def report():
    for path, (run, verdict) in VERDICTS.items():
        found = [t for t in TARGETS if blob(verdict, t) is not None and blob(verdict, t) != blob(MEASURED, t)]
        absent = [t for t in TARGETS if blob(verdict, t) is None]
        print(path, run, verdict, "differ:", ",".join(found) or "none", "absent:", ",".join(absent) or "none",
              "sentence:", "yes" if found else "no")


if __name__ == "__main__":
    if sys.argv[1] == "report":
        report()
    elif sys.argv[1] == "apply":
        for path, earlier in PACKS.items():
            content = expected(path, earlier)
            open(path, "w", encoding="utf-8", newline="").write(content)
    else:
        sys.exit(verify())

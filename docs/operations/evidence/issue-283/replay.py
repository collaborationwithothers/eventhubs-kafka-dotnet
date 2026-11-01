#!/usr/bin/env python3
"""Replays the 103 C5 survivors of run 41232286130 in shards p5 and p6 as source edits (issue 283).

Diagnostic evidence only; never C5 acceptance. From the repository root, on any commit whose
target file at HEAD equals the source text both reports recorded; run stops otherwise:
  replay.py run P5_REPORT P6_REPORT OUTPUT_JSONL K/N [ID ...]   replay every Nth survivor from K (0-based)
  replay.py check OUTPUT_JSONL...                               compare results with the disposition table
P5_REPORT and P6_REPORT are stryker/reports/mutation-report.json from artifacts 13056620559 and
13054686889. Write output outside the repository. run replays the tree committed at HEAD
(git archive HEAD), not the working copy, and runs the tests committed there, which include the
issue 283 tests.
"""
import hashlib, json, os, re, subprocess, sys, tempfile

TARGET = "VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs"
REPORTS = (("p5", "16059420267e2fe864a7e3278793edfa54b4ca76102fa47136e3f2958728b820", (40525, 52156), 83),
           ("p6", "1b1613c74a53c6fb8861b00fb5cea1bf4a119ec801812a1ddcbe6551e22164c7", (52156, 2147483647), 20))
# Stryker replaces a syntax node; parentheses keep an expression replacement one node. A compound
# assignment is an expression statement here, and C# does not accept one in parentheses.
PLAIN = ("Statement", "Block", "String", "Linq", "AndAssignmentExpression")
# The report prints a logical mutant without the parentheses of its left operand, so a mutated
# `a || b || c` reads as `a || b && c`. Stryker's node is `(a || b) && c`; run rebuilds that form.
OPERATORS = ("||", "&&", " or ", " and ")
TEST = ["dotnet", "test", "--project", "tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj", "-c", "Release",
        "-p:TreatWarningsAsErrors=false", "-p:WarningsAsErrors=", "-p:CodeAnalysisTreatWarningsAsErrors=false"]
MEASURED = "2dc9715e3de176f7e1cd55eee9c251e43c29c746"
TABLE = "docs/operations/verifier-mutation.md"
HEADING = "## Issue 283 survivor dispositions"
NOTICE = ("run replays the tests committed at HEAD, which may differ from the tests of run 41232286130; a difference "
          "from the recorded dispositions is expected when the verifier source or tests have changed since that run")
PREFIX = "Parchwell.Verifier.Tests.SingleAction.ProbeV2AccessEvidenceAuditTests."
ALIASES = {f"AE{number}": PREFIX + name for number, name in enumerate([
    "Given_EnvelopeDefect_When_Checked_Then_ExactProblemIsReturned",
    "Given_StatusOrLimitDefect_When_Checked_Then_ExactProblemsAreReturned",
    "Given_CoverageDefect_When_Checked_Then_WireIsRejected",
    "Given_CompleteStatus_When_CoverageIsChecked_Then_EveryCollectionMustBeComplete",
    "Given_IncompleteAuditWithEntryDefect_When_Checked_Then_WireIsRejected",
    "Given_IncompleteAuditWithUnsupportedGrant_When_Checked_Then_ClosedVocabularyApplies",
    "Given_MismatchWithMatchingTable_When_Checked_Then_OnlyUnexpectedGrantExplainsIt",
    "Given_ProjectionBoundsCase_When_Checked_Then_CapturePrecedesWireParsing"], start=1)}


def survivors(p5, p6):
    found, sources = [], set()
    for path, (shard, digest, span, expected) in zip((p5, p6), REPORTS):
        raw = open(path, "rb").read()
        if hashlib.sha256(raw).hexdigest() != digest:
            sys.exit(f"{shard}: report hash mismatch")
        entry = [e for p, e in json.loads(raw)["files"].items() if p.replace("\\", "/").endswith(TARGET)]
        if len(entry) != 1:
            sys.exit(f"{shard}: expected one entry for {TARGET}, found {len(entry)}")
        mutants = sorted((m for m in entry[0]["mutants"] if m["status"] == "Survived"), key=lambda m: int(m["id"]))
        if len(mutants) != expected:
            sys.exit(f"{shard}: expected {expected} survivors, found {len(mutants)}")
        sources.add(entry[0]["source"])
        found += [(span, m) for m in mutants]
    if len(sources) != 1:
        sys.exit("the two reports measured different source text")
    return sources.pop(), found


def logical(original, replacement):
    start = next(i for i, (a, b) in enumerate(zip(original, replacement)) if a != b)
    for old in OPERATORS:
        k = original.rfind(old, 0, start + len(old))
        if k != -1 and k <= start < k + len(old):
            new = next(x for x in OPERATORS if replacement.startswith(x, k) and x != old)
            if original[k + len(old):] != replacement[k + len(new):]:
                break
            return "(" + original[:k].rstrip() + ") " + new.strip() + " " + original[k + len(old):].lstrip()
    sys.exit(f"cannot rebuild logical mutant from {replacement!r}")


def run(p5, p6, output, part, ids):
    print(NOTICE, flush=True)
    source, found = survivors(p5, p6)
    k, n = (int(x) for x in part.split("/"))
    work = tempfile.mkdtemp(prefix="issue-283-replay-")
    subprocess.run(f"git archive HEAD | tar -x -C {work}", shell=True, check=True)
    if open(os.path.join(work, "src/Parchwell.Verifier", TARGET), newline="").read() != source:
        sys.exit(f"{TARGET} at HEAD differs from the source the reports measured; the mutant locations no longer apply")
    env = dict(os.environ, ParchwellVerifierMutationOnly="true")
    for span, mutant in found[k::n]:
        if ids and mutant["id"] not in ids:
            continue
        path = os.path.join(work, "src/Parchwell.Verifier", TARGET)
        original = open(path, newline="").read()
        lines = original.split("\n")
        def offset(position):
            return sum(len(x) + 1 for x in lines[:position["line"] - 1]) + position["column"] - 1
        start, end = offset(mutant["location"]["start"]), offset(mutant["location"]["end"])
        if not span[0] <= start < end <= span[1]:
            sys.exit(f"{mutant['id']}: outside span {span}")
        replacement = mutant["replacement"]
        if mutant["mutatorName"] == "Logical mutation":
            replacement = logical(original[start:end], replacement)
        if not mutant["mutatorName"].startswith(PLAIN):
            replacement = "(" + replacement + ")"
        open(path, "w", newline="").write(original[:start] + replacement + original[end:])
        try:
            result = subprocess.run(TEST, cwd=work, env=env, capture_output=True, text=True)
        finally:
            open(path, "w", newline="").write(original)
        text = result.stdout + result.stderr
        failed = sorted({re.sub(r"\(.*", "", x) for x in re.findall(r"^failed (\S+)", text, re.M)})
        status = "compile_error" if re.search(r"error CS\d+", text) else ("killed" if result.returncode else "survived")
        with open(output, "a", encoding="utf-8") as sink:
            sink.write(json.dumps({"id": mutant["id"], "status": status, "failed_methods": failed}, sort_keys=True) + "\n")
        print(mutant["id"], status, len(failed), flush=True)


def added_classes():
    """Test classes in files added after the measured commit; a test that did not exist then counts as new."""
    names = subprocess.run(["git", "diff", "--name-only", "--diff-filter=A", MEASURED, "HEAD", "--", "tests/Parchwell.Verifier.Tests"],
                           capture_output=True, text=True, check=True).stdout.split()
    return {os.path.basename(x)[:-3] for x in names if x.endswith(".cs")}


def check(outputs):
    added = added_classes()
    if ALIASES["AE1"].split(".")[-2] not in added:
        sys.exit("the issue 283 test class is not in a file added after the measured commit")
    section = open(TABLE, encoding="utf-8").read().split(HEADING, 1)[1].split("\n## ", 1)[0]
    rows = {r[0]: (r[3], r[4]) for r in (re.split(r"\s*\|\s*", line.strip())[1:-1] for line in section.splitlines())
            if len(r) == 5 and r[0].isdigit()}
    results = {}
    for output in outputs:
        for line in open(output, encoding="utf-8"):
            record = json.loads(line)
            results[record["id"]] = record
    problems = [f"{i}: missing" for i in rows if i not in results] + [f"{i}: not in table" for i in results if i not in rows]
    for i, (disposition, reference) in rows.items():
        record = results.get(i)
        if record is None:
            continue
        failed = set(record["failed_methods"])
        new = {x for x in failed if x.split(".")[-2] in added}
        if disposition == "missing test":
            ok = record["status"] == "killed" and ALIASES.get(reference) in failed and failed == new
        elif disposition == "equivalent":
            ok = record["status"] == "survived"
        elif disposition == "killed on replay":
            ok = record["status"] == "killed" and bool(failed - new)
        else:
            ok = False
        if not ok:
            problems.append(f"{i}: {disposition} {reference} but {record['status']} {sorted(failed)}")
    print(f"rows {len(rows)} results {len(results)} problems {len(problems)}")
    print("\n".join(problems))
    return 1 if problems else 0


if __name__ == "__main__":
    if sys.argv[1] == "run":
        run(sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5], set(sys.argv[6:]))
    else:
        sys.exit(check(sys.argv[2:]))

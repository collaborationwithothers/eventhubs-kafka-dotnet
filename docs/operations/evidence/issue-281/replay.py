#!/usr/bin/env python3
"""Replays the 63 C5 survivors of run 41232286130 in shard p3 as source edits (issue 281).

Diagnostic evidence only; never C5 acceptance. From the repository root, on any commit whose
target file at HEAD equals the source text the report recorded; run stops otherwise:
  replay.py run REPORT OUTPUT_JSONL K/N [ID ...]   replay every Nth survivor from K (0-based)
  replay.py check OUTPUT_JSONL...                  compare results with the disposition table
REPORT is stryker/reports/mutation-report.json from artifact 13056428027. Write output
outside the repository. run replays the tree committed at HEAD (git archive HEAD), not the
working copy, and runs the tests committed there, which include the issue 281 tests.
"""
import hashlib, json, os, re, subprocess, sys, tempfile

TARGET = "VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs"
DIGEST = "2ff2f83ba833de08d4652fe83f24ef4041554b30e689bd2e47864d777c76ff8b"
SPAN = (20406, 31533)
EXPECTED = 63
# Stryker replaces a syntax node; parentheses keep an expression replacement one node. A compound
# assignment is an expression statement here, and C# does not accept one in parentheses.
PLAIN = ("Statement", "Block", "String", "Linq", "OrAssignmentExpression")
TEST = ["dotnet", "test", "--project", "tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj", "-c", "Release",
        "-p:TreatWarningsAsErrors=false", "-p:WarningsAsErrors=", "-p:CodeAnalysisTreatWarningsAsErrors=false"]
TABLE = "docs/operations/verifier-mutation.md"
HEADING = "## Issue 281 survivor dispositions"
NOTICE = ("run replays the tests committed at HEAD, which may differ from the tests of run 41232286130; a difference "
          "from the recorded dispositions is expected when the verifier source or tests have changed since that run")
PREFIX = "Parchwell.Verifier.Tests.SingleAction.ProbeV2CandidateProjectionAuditTests."
ALIASES = {f"CP{number}": PREFIX + name for number, name in enumerate([
    "Given_PermissionAuditOutcome_When_Projected_Then_EveryRepeatCarriesItsProblem",
    "Given_InvalidHelperSlots_When_Projected_Then_AnyInvalidHelperInvalidatesTheRepeat",
    "Given_CompleteMatrix_When_Projected_Then_HelperClientsAndOnlyTheSeedReferenceAreKept",
    "Given_SourceOrReaderDefect_When_Projected_Then_FirstRepeatHasExactStatusAndReasons",
    "Given_NegativeReaderSourceActionFailed_When_Projected_Then_ControlsGapIsRecorded",
    "Given_NegativeSendDelivered_When_Projected_Then_SecurityFindingFollowsActionFidelity",
    "Given_FinalConfiguration_When_PriorsChecked_Then_StatusDiagnosticsAndSecurityAreExact"], start=1)}


def report(path):
    raw = open(path, "rb").read()
    if hashlib.sha256(raw).hexdigest() != DIGEST:
        sys.exit("report hash mismatch")
    entry = [e for p, e in json.loads(raw)["files"].items() if p.replace("\\", "/").endswith(TARGET)]
    if len(entry) != 1:
        sys.exit(f"expected one entry for {TARGET}, found {len(entry)}")
    found = sorted((m for m in entry[0]["mutants"] if m["status"] == "Survived"), key=lambda m: int(m["id"]))
    if len(found) != EXPECTED:
        sys.exit(f"expected {EXPECTED} survivors, found {len(found)}")
    return entry[0]["source"], found


def run(report_path, output, part, ids):
    print(NOTICE, flush=True)
    source, found = report(report_path)
    k, n = (int(x) for x in part.split("/"))
    work = tempfile.mkdtemp(prefix="issue-281-replay-")
    subprocess.run(f"git archive HEAD | tar -x -C {work}", shell=True, check=True)
    if open(os.path.join(work, "src/Parchwell.Verifier", TARGET), newline="").read() != source:
        sys.exit(f"{TARGET} at HEAD differs from the source the report measured; the mutant locations no longer apply")
    env = dict(os.environ, ParchwellVerifierMutationOnly="true")
    for mutant in found[k::n]:
        if ids and mutant["id"] not in ids:
            continue
        path = os.path.join(work, "src/Parchwell.Verifier", TARGET)
        original = open(path, newline="").read()
        lines = original.split("\n")
        def offset(position):
            return sum(len(x) + 1 for x in lines[:position["line"] - 1]) + position["column"] - 1
        start, end = offset(mutant["location"]["start"]), offset(mutant["location"]["end"])
        if not SPAN[0] <= start < end <= SPAN[1]:
            sys.exit(f"{mutant['id']}: outside span {SPAN}")
        replacement = mutant["replacement"]
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


def check(outputs):
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
        new = {x for x in failed if x.startswith(PREFIX)}
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
        run(sys.argv[2], sys.argv[3], sys.argv[4], set(sys.argv[5:]))
    else:
        sys.exit(check(sys.argv[2:]))

#!/usr/bin/env python3
"""Replays the 30 C5 survivors and the one Timeout of run 41232286130 as source edits (issue 275).

Diagnostic evidence only; never C5 acceptance. From the repository root:
  replay.py map NEW_DIR OLD_DIR            compare each selected mutant with the issue 268 reports
  replay.py run NEW_DIR OUTPUT_JSONL K/N   replay every Nth selected mutant from K (0-based)
  replay.py check OUTPUT_JSONL...          compare results with the disposition table
NEW_DIR holds phase.json, publication.json and repeat-p1.json to repeat-p3.json from
run 41232286130; OLD_DIR holds phase.json, publication.json and repeat.json from run
41183628251. Write output outside the repository.
"""
import hashlib, json, os, re, signal, subprocess, sys, tempfile, threading, time

MEASURED = "2dc9715e3de176f7e1cd55eee9c251e43c29c746"
PHASE = "Judge/PhaseJudge.cs"
PUBLICATION = "VerifyRun/Publication/PublicationAcceptance.cs"
REPEAT = "VerifyRun/SingleAction/SingleActionRepeatJudgement.cs"
NEW = {
    "phase": (PHASE, "3e65fdcd054381d819fef74334c4370908f8fe8bf2102ac6bffb997b07c6b17b"),
    "publication": (PUBLICATION, "b6061ee9065a2a28270a9d27702b4796c7b63b6805a05d887e3d4225b0ca67aa"),
    "repeat-p1": (REPEAT, "b438dfbf0a105f547bb4140b9feb1574b166bccae0bc53603e6e28233adf478e"),
    "repeat-p2": (REPEAT, "962d0f4724d0838e4831257fb01058709bc237736cbf818c1fae3ab3e041d276"),
    "repeat-p3": (REPEAT, "86b48d2c064634e2725a3c9015d8813305100f735f7cc78020f9703fae271bbf"),
}
OLD = {
    "phase": (PHASE, "51c2b2bf4c26262cabd2f0da5c9f8db910f6b84c614d3d85b3ec2a5f8e4c2be7"),
    "publication": (PUBLICATION, "262ffb95e6a1a1f8362632d4b258b69354ff63013957ac47b2aab3a5fa241ac2"),
    "repeat": (REPEAT, "9056569722f55b72f3f58ff8ffc6c51ffe5cb84f5e803b9e3a612a6a54ff22eb"),
}
EXPECTED = {PHASE: 16, PUBLICATION: 6, REPEAT: 8}
TIMEOUTS = ["4863"]
# Stryker replaces a syntax node; parentheses keep an expression replacement one node.
PLAIN = ("Statement", "Block", "String", "Linq", "PostIncrement")
# The report prints this node without inner parentheses; issue 268 replay.py carries the same form.
OVERRIDES = {"14286": "(FormatException or JsonException) and InvalidOperationException"}
TEST = ["dotnet", "test", "--project", "tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj", "-c", "Release",
        "-p:TreatWarningsAsErrors=false", "-p:WarningsAsErrors=", "-p:CodeAnalysisTreatWarningsAsErrors=false"]
# A Timeout mutant runs with a 4 GiB GC heap limit. It stops once a failed test reports OutOfMemoryException
# thrown through PhaseJudge.Evaluate, or after LIMIT seconds.
HEAP = "0x100000000"
LIMIT = 900
TABLE = "docs/operations/verifier-mutation.md"
HEADING = "## Issue 275 survivor dispositions"


def entries(report_dir, reports):
    found = {}
    for shard, (target, digest) in reports.items():
        raw = open(os.path.join(report_dir, shard + ".json"), "rb").read()
        if hashlib.sha256(raw).hexdigest() != digest:
            sys.exit(f"{shard}: report hash mismatch")
        for path, entry in json.loads(raw)["files"].items():
            if path.replace("\\", "/").endswith(target):
                found.setdefault(target, []).append(entry)
    return found


def survivors(report_dir):
    found, timeouts = [], []
    for target, files in entries(report_dir, NEW).items():
        ids = {m["id"] for entry in files for m in entry["mutants"] if m["status"] == "Survived"}
        late = {m["id"] for entry in files for m in entry["mutants"] if m["status"] == "Timeout"}
        mutants = {m["id"]: m for m in files[0]["mutants"]}
        found += [(target, mutants[i], "Survived") for i in sorted(ids, key=int)]
        timeouts += [(target, mutants[i], "Timeout") for i in sorted(late, key=int)]
    counts = {t: sum(1 for x, _, _ in found if x == t) for t in EXPECTED}
    if counts != EXPECTED:
        sys.exit(f"expected {EXPECTED}, found {counts}")
    if [m["id"] for _, m, _ in timeouts] != TIMEOUTS:
        sys.exit(f"expected Timeout {TIMEOUTS}, found {[m['id'] for _, m, _ in timeouts]}")
    return found + timeouts


def key(mutant):
    return mutant["mutatorName"], mutant["replacement"], json.dumps(mutant["location"], sort_keys=True)


def map_old(new_dir, old_dir):
    old = {t: (files[0]["source"], {m["id"]: m for m in files[0]["mutants"]}) for t, files in entries(old_dir, OLD).items()}
    new = entries(new_dir, NEW)
    problems = [f"{t}: source differs" for t, files in new.items() for e in files if e["source"] != old[t][0]]
    for target, mutant, _ in survivors(new_dir):
        before = old[target][1].get(mutant["id"])
        if before is None or key(before) != key(mutant):
            problems.append(f"{mutant['id']}: not the same mutant in run 41183628251")
        else:
            print(mutant["id"], "run 41183628251 status", before["status"])
    print(f"problems {len(problems)}")
    print("\n".join(problems))
    return 1 if problems else 0


def run(report_dir, output, part):
    if subprocess.run(["git", "diff", "--quiet", MEASURED, "HEAD", "--", "src", "tests"]).returncode:
        sys.exit(f"src or tests differ from {MEASURED}")
    k, n = (int(x) for x in part.split("/"))
    work = tempfile.mkdtemp(prefix="issue-275-replay-")
    subprocess.run(f"git archive HEAD | tar -x -C {work}", shell=True, check=True)
    env = dict(os.environ, ParchwellVerifierMutationOnly="true")
    for target, mutant, measured in survivors(report_dir)[k::n]:
        path = os.path.join(work, "src/Parchwell.Verifier", target)
        original = open(path, newline="").read()
        lines = original.split("\n")
        def offset(position):
            return sum(len(x) + 1 for x in lines[:position["line"] - 1]) + position["column"] - 1
        start, end = offset(mutant["location"]["start"]), offset(mutant["location"]["end"])
        replacement = OVERRIDES.get(mutant["id"], mutant["replacement"])
        if not mutant["mutatorName"].startswith(PLAIN):
            replacement = "(" + replacement + ")"
        open(path, "w", newline="").write(original[:start] + replacement + original[end:])
        try:
            if measured == "Timeout":
                text, code = bounded(work, dict(env, DOTNET_GCHeapHardLimit=HEAP))
            else:
                result = subprocess.run(TEST, cwd=work, env=env, capture_output=True, text=True)
                text, code = result.stdout + result.stderr, result.returncode
        finally:
            open(path, "w", newline="").write(original)
        failed = sorted({re.sub(r"\(.*", "", x) for x in re.findall(r"^failed (\S+)", text, re.M)})
        if code is None:
            status = "timeout"
        else:
            status = "compile_error" if re.search(r"error CS\d+", text) else ("killed" if code else "survived")
        with open(output, "a", encoding="utf-8") as sink:
            record = {"id": mutant["id"], "status": status, "failed_methods": failed}
            if measured == "Timeout":
                record["phase_judge_out_of_memory"] = phase_judge_out_of_memory(text)
            sink.write(json.dumps(record, sort_keys=True) + "\n")
        print(mutant["id"], status, len(failed), flush=True)


def phase_judge_out_of_memory(text):
    blocks = re.split(r"(?m)^(?=failed )", text)[1:]
    return any("System.OutOfMemoryException" in x and "PhaseJudge.Evaluate" in x for x in blocks)


def bounded(work, env):
    """Runs the tests until phase_judge_out_of_memory holds or LIMIT s pass; returns (output, exit code or None)."""
    process = subprocess.Popen(TEST, cwd=work, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
                               start_new_session=True)
    lines = []
    threading.Thread(target=lambda: lines.extend(process.stdout), daemon=True).start()
    deadline, code = time.monotonic() + LIMIT, None
    while time.monotonic() < deadline and process.poll() is None:
        if phase_judge_out_of_memory("".join(list(lines))):
            code = 1
            break
        time.sleep(1)
    if process.poll() is None:
        os.killpg(process.pid, signal.SIGKILL)
    else:
        code = process.returncode if code is None else code
    process.wait()
    time.sleep(1)
    return "".join(list(lines)), code


def check(outputs):
    section = open(TABLE, encoding="utf-8").read().split(HEADING, 1)[1].split("\n## ", 1)[0]
    rows = {r[1]: (r[4], r[5]) for r in (re.split(r"\s*\|\s*", line.strip())[1:-1] for line in section.splitlines())
            if len(r) == 6 and r[1].isdigit()}
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
        if disposition == "equivalent":
            ok = record["status"] == ("compile_error" if reference == "E11" else "survived")
        elif disposition == "killed on replay":
            ok = record["status"] == "killed" and bool(record["failed_methods"])
            if reference == "T1":
                ok = ok and record.get("phase_judge_out_of_memory") is True
        else:
            ok = False
        if not ok:
            problems.append(f"{i}: {disposition} {reference} but {record['status']} {len(record['failed_methods'])}")
    print(f"rows {len(rows)} results {len(results)} problems {len(problems)}")
    print("\n".join(problems))
    return 1 if problems else 0


if __name__ == "__main__":
    if sys.argv[1] == "map":
        sys.exit(map_old(sys.argv[2], sys.argv[3]))
    if sys.argv[1] == "run":
        run(sys.argv[2], sys.argv[3], sys.argv[4])
    else:
        sys.exit(check(sys.argv[2:]))

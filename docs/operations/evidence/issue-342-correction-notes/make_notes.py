"""Adds the issue 342 correction notes and pointers to the 14 affected results and checks them.

Usage, from the repository root, with python3 -I:
  make_notes.py check-artifacts <empty directory>
  make_notes.py apply <YYYY-MM-DD>
  make_notes.py verify <YYYY-MM-DD>
"""
import json
import os
import re
import subprocess
import sys
import zipfile

REPO = "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet"
API = "repos/collaborationwithothers/eventhubs-kafka-dotnet"
MAIN = "87327f5841822a8962dd3dbaecb2ee442a8faa8b"
OLD_GATE = "41232286130"
LEAK_GATE = "41484094658"
CORRECTED = "41670913768"
SURVIVOR_COMPARISON = REPO + "/issues/333#issuecomment-6600991848"
VERIFICATION_COMPARISON = REPO + "/issues/340#issuecomment-6611864147"
TARGETS = [
    "Judge/PhaseJudge.cs",
    "VerifyRun/Publication/PublicationAcceptance.cs",
    "VerifyRun/A01/A01EvidenceRecorder.cs",
    "VerifyRun/A01/WorkloadRecorder.cs",
    "VerifyRun/SingleAction/SingleActionRepeatJudgement.cs",
    "VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs",
    "VerifyRun/IdleObservation/IdleObservationJudgement.cs",
]
SA = ["VerifyRun/SingleAction/SingleActionRepeatJudgement.cs", "VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs"]
IOJ = ["VerifyRun/IdleObservation/IdleObservationJudgement.cs"]
INPUTS_GONE = "inputs_not_retained"
NO_OFFLINE = "no_offline_verifier_mode"

# result: (file stem, current gate, json key, json gate, source commit, differ, absent, rejudge, artifact ids)
RESULTS = {
    "A01": ("2026-10-25", OLD_GATE, "mutation_evidence", "40233552992", "fa417d783e833ad222ab5173978433448e4aefbe",
            ["Judge/PhaseJudge.cs"], SA + IOJ, INPUTS_GONE, [12613440446, 12613590239]),
    "A03": ("2026-11-02", LEAK_GATE, "mutation_gate", LEAK_GATE, "9014b8effd4f8556d3446bcfbf8f951a52905bb2",
            IOJ, [], INPUTS_GONE, [13187681970, 13188401369]),
    "B01": ("2026-10-27", OLD_GATE, "mutation_gate", "40415248275", "7b6834c5e8668e89376597bc2b524c0725326daf",
            SA, IOJ, INPUTS_GONE, [12715895816, 12715517221]),
    "B03": ("2026-10-27", OLD_GATE, "mutation_gate", "40415248275", "7b6834c5e8668e89376597bc2b524c0725326daf",
            SA, IOJ, INPUTS_GONE, [12715895816, 12715517221]),
    "B04": ("2026-10-26", OLD_GATE, "mutation_evidence", "40315208371", "cb83254f1c2645acd86fe734b24e542e0956edd3",
            [], SA + IOJ, INPUTS_GONE, [12648552841, 12648832668, 12655032300, 12654947747]),
    "B05": ("2026-10-27", OLD_GATE, "mutation_gate", "40415248275", "7b6834c5e8668e89376597bc2b524c0725326daf",
            SA, IOJ, INPUTS_GONE, [12715895816, 12715517221]),
    "B08": ("2026-11-01", OLD_GATE, "mutation_gate", OLD_GATE, "8078e6fcc16b1115899fbfb3edfbb1299cb8eaf1",
            [], IOJ, NO_OFFLINE, [13078662873, 13078313354]),
    "C08": ("2026-10-28", OLD_GATE, "mutation", "40642331044", "20f17794330b60d77a75bf5788ee442cc8baa1f6",
            SA, IOJ, INPUTS_GONE, [12834018507, 12835061323]),
    "C12": ("2026-10-27", OLD_GATE, "mutation_gate", "40415248275", "7b6834c5e8668e89376597bc2b524c0725326daf",
            SA, IOJ, INPUTS_GONE, [12715895816, 12715517221]),
    "E01": ("2026-11-01", OLD_GATE, "mutation_gate", OLD_GATE, "8078e6fcc16b1115899fbfb3edfbb1299cb8eaf1",
            [], IOJ, NO_OFFLINE, [13078662873, 13078313354]),
    "F01": ("2026-10-31", OLD_GATE, "mutation_gate", OLD_GATE, "b79acb256d09ea29e493acc460ebbe2c9c59aa4f",
            [], IOJ, NO_OFFLINE, [13044674034, 13044719046]),
    "F02": ("2026-11-01", OLD_GATE, "mutation_gate", OLD_GATE, "99131dee48677b971f0b2299d243779151ebe700",
            [], IOJ, NO_OFFLINE, [13094681168, 13095030533]),
    "F04": ("2026-10-28", OLD_GATE, "mutation_gate", "40642331044", "20f17794330b60d77a75bf5788ee442cc8baa1f6",
            SA, IOJ, INPUTS_GONE, [12834018507, 12835061323]),
    "F06": ("2026-11-01", OLD_GATE, "mutation_gate", OLD_GATE, "961413f3fc81ec1b58b3fecc7f9e5122a8a21010",
            [], IOJ, NO_OFFLINE, [13082448520, 13082618179]),
}
MARKER = re.compile(rb"^PARCHWELL_[A-Z0-9_]*(CAPTURE|REGISTRATION|LEDGER)", re.M)
INPUT_NAMES = {"capture.json", "registration.json", "completion.json"}

REJUDGE_TEXT = {
    INPUTS_GONE: ("Offline re-judge: not run, recorded as a gap, because the judge inputs were in ephemeral blob storage, "
                  "destroyed when the run's environment was destroyed; the retained artifacts hold judge outputs, pod logs "
                  "and pod JSON only."),
    NO_OFFLINE: ("Offline re-judge: not run, recorded as a gap, because the verifier cannot read these inputs offline: "
                 "it needs mounted probe inputs and workload identity, and writes only to blob storage."),
}


def run(*args):
    return subprocess.run(args, check=True, capture_output=True).stdout


def blob(commit, target):
    result = subprocess.run(["git", "rev-parse", f"{commit}:src/Parchwell.Verifier/{target}"], capture_output=True, text=True)
    return result.stdout.strip() if result.returncode == 0 else None


def gate_runs(text):
    match = re.search(r"^## Mutation publication gate\n(.*?)(?=^## |\Z)", text, re.S | re.M)
    if not match:
        return None
    current = re.search(r"Current gate:[^\n]*?actions/runs/(\d+)", match.group(1))
    return current.group(1) if current else ""


def check_classification():
    found = {}
    for item in sorted(os.listdir("results")):
        for name in sorted(os.listdir(os.path.join("results", item))):
            if name.endswith(".md"):
                current = gate_runs(open(os.path.join("results", item, name), encoding="utf-8").read())
                if current is not None:
                    found[item] = (name[:-3], current)
    if len(found) > len(RESULTS):
        sys.exit(f"affected list grew to {len(found)}: {sorted(found)}; push -wip and hand off to the next session")
    expected = {item: (row[0], row[1]) for item, row in RESULTS.items()}
    if found != expected:
        sys.exit(f"classification differs: {found}")


def check_blobs():
    for item, row in RESULTS.items():
        source, differ, absent = row[4], row[5], row[6]
        got_absent = [t for t in TARGETS if blob(source, t) is None]
        got_differ = [t for t in TARGETS if blob(source, t) is not None and blob(source, t) != blob(MAIN, t)]
        if got_absent != absent or got_differ != differ or any(blob(MAIN, t) is None for t in TARGETS):
            sys.exit(f"{item}: blob comparison differs: differ {got_differ}, absent {got_absent}")
    config = json.loads(run("git", "show", f"{MAIN}:tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json"))
    if config["stryker-config"]["mutate"] != TARGETS:
        sys.exit("mutate list at 87327f5 differs from TARGETS")


def check_artifacts(directory):
    if os.listdir(directory):
        sys.exit("artifact directory is not empty")
    for item, row in RESULTS.items():
        markers = 0
        for artifact in row[8]:
            path = os.path.join(directory, f"{artifact}.zip")
            if not os.path.exists(path):
                with open(path, "wb") as stream:
                    stream.write(run("gh", "api", f"{API}/actions/artifacts/{artifact}/zip"))
            with zipfile.ZipFile(path) as archive:
                for member in archive.namelist():
                    if os.path.basename(member) in INPUT_NAMES:
                        sys.exit(f"{item}: artifact {artifact} holds {member}")
                    markers += len(MARKER.findall(archive.read(member)))
        if (markers == 0) != (row[7] == INPUTS_GONE):
            sys.exit(f"{item}: {markers} input marker lines, rejudge class {row[7]}")
        print(item, "marker lines", markers)
    print("artifacts ok")


def files_text(source, differ, absent):
    same = [t for t in TARGETS if t not in differ and t not in absent]
    parts = [f"Of the seven files that run {CORRECTED} mutated, {len(same)} are byte-identical between this result's "
             f"source `{source}` and `{MAIN}`: " + ", ".join(f"`{t}`" for t in same) + "."]
    if differ:
        parts.append("These differ: " + ", ".join(f"`{t}`" for t in differ) + ".")
    if absent:
        parts.append(f"These did not exist at `{source}`: " + ", ".join(f"`{t}`" for t in absent) + ".")
    parts.append(f"So run {CORRECTED} did not measure exactly the verifier source that produced this result.")
    return " ".join(parts)


def note(item, date):
    stem, gate, key, json_gate, source, differ, absent, rejudge, _ = RESULTS[item]
    lines = [f"## Correction note, {date}", "",
             "This note corrects the mutation publication gate above. It changes no verdict word. The only other text added to "
             "this report is the pointer to this note under its opening paragraph.", ""]
    if gate == LEAK_GATE:
        lines.append(f"- The current gate, [C5 run {gate}]({REPO}/actions/runs/{gate}), overstated the strength of the verifier's tests "
                     f"because of static-field state leakage: before [pull request 341]({REPO}/pull/341), a shard whose span held a "
                     "static-field mutant could report every mutant as killed, because the mutated static value persisted in the reused "
                     f"test process ([survivor comparison]({SURVIVOR_COMPARISON})).")
    else:
        lines.append(f"- The current gate, [C5 run {gate}]({REPO}/actions/runs/{gate}), ran before [pull request 341]({REPO}/pull/341). "
                     "Its shards contained static-field mutants of the kind that caused the state leakage. Leakage was measured only "
                     f"for [C5 run {LEAK_GATE}]({REPO}/actions/runs/{LEAK_GATE}) ([survivor comparison]({SURVIVOR_COMPARISON})), "
                     f"not for run {gate}.")
    if json_gate != gate:
        lines.append(f"- The field `publication.{key}` of [{stem}.json]({stem}.json) cites [C5 run {json_gate}]({REPO}/actions/runs/{json_gate}). "
                     f"The current gate is run {gate}, as this report states. This note does not assess run {json_gate}.")
    lines.append(f"- The corrected measurement is [C5 run {CORRECTED}]({REPO}/actions/runs/{CORRECTED}), which measured `main` at "
                 f"`{MAIN}`, after pull request 341: 3668 detected out of 3813 scored mutants (96.2%), "
                 f"`measurement_complete: true` and `break_passed: true` ([verification comparison]({VERIFICATION_COMPARISON})).")
    lines.append("- " + files_text(source, differ, absent))
    lines.append("- The verdict word is unchanged, because it comes from the run data, not from the mutation score.")
    lines.append("- " + REJUDGE_TEXT[rejudge])
    return "\n".join(lines) + "\n"


def field(item, date):
    stem, gate, key, json_gate, source, differ, absent, rejudge, _ = RESULTS[item]
    return {
        "date": date,
        "note_heading": f"Correction note, {date}",
        "verdict_changed": False,
        "current_gate_run": f"{REPO}/actions/runs/{gate}",
        "json_gate_field": f"publication.{key}",
        "json_gate_run": f"{REPO}/actions/runs/{json_gate}",
        "static_field_mutants_in_current_gate": True,
        "leakage_measured_in_current_gate": gate == LEAK_GATE,
        "comparisons": [SURVIVOR_COMPARISON, VERIFICATION_COMPARISON],
        "corrected_run": f"{REPO}/actions/runs/{CORRECTED}",
        "corrected_measured_commit": MAIN,
        "corrected_detected": 3668,
        "corrected_scored": 3813,
        "corrected_measurement_complete": True,
        "corrected_break_passed": True,
        "result_source_commit": source,
        "judge_files_identical": [t for t in TARGETS if t not in differ and t not in absent],
        "judge_files_differ": differ,
        "judge_files_absent_at_source": absent,
        "rejudge": {"outcome": "gap", "reason": rejudge},
    }


def pointer(date):
    return (f"A [correction note about the mutation publication gate](#correction-note-{date}) "
            f"was added on {date}.\n\n")


def opening_end(item, old_md):
    lines = old_md.split(b"\n")
    if (len(lines) < 6 or not lines[0].startswith(b"# ") or lines[1] != b"" or lines[2] == b""
            or lines[2].startswith(b"#") or lines[3] != b"" or lines[4] == b""):
        sys.exit(f"{item}: Markdown does not open with a title, one one-line paragraph and a blank line")
    if b"## Correction note" in old_md:
        sys.exit(f"{item}: Markdown already holds a correction note")
    return sum(len(line) + 1 for line in lines[:4])


def paths(item):
    stem = RESULTS[item][0]
    return os.path.join("results", item, stem + ".md"), os.path.join("results", item, stem + ".json")


def head(path):
    return run("git", "show", f"{MAIN}:{path}")


def apply(date):
    for item in RESULTS:
        md, js = paths(item)
        old_md, old_js = head(md), head(js)
        if open(md, "rb").read() != old_md or open(js, "rb").read() != old_js:
            sys.exit(f"{item}: working file differs from {MAIN}")
        at = opening_end(item, old_md)
        with open(md, "wb") as stream:
            stream.write(old_md[:at] + pointer(date).encode("ascii") + old_md[at:] + b"\n" + note(item, date).encode("ascii"))
        body = json.dumps(field(item, date), indent=2, ensure_ascii=True).replace("\n", "\n  ")
        if not old_js.endswith(b"\n}\n"):
            sys.exit(f"{item}: unexpected JSON ending")
        with open(js, "wb") as stream:
            stream.write(old_js[:-3] + b',\n  "mutation_gate_correction": ' + body.encode("ascii") + b"\n}\n")


def verify(date):
    for item in RESULTS:
        md, js = paths(item)
        old_md, old_js = head(md), head(js)
        new_md, new_js = open(md, "rb").read(), open(js, "rb").read()
        at = opening_end(item, old_md)
        added_pointer, added_note = pointer(date).encode("ascii"), b"\n" + note(item, date).encode("ascii")
        if new_md[at:at + len(added_pointer)] != added_pointer or not new_md.endswith(added_note):
            sys.exit(f"{item}: Markdown pointer or note differs")
        if new_md[:at] + new_md[at + len(added_pointer):len(new_md) - len(added_note)] != old_md:
            sys.exit(f"{item}: Markdown differs from {MAIN} apart from the pointer and the note")
        if not new_js.startswith(old_js[:-3]):
            sys.exit(f"{item}: JSON prefix changed")
        parsed = json.loads(new_js)
        added = parsed.pop("mutation_gate_correction")
        if parsed != json.loads(old_js) or added != field(item, date) or list(json.loads(new_js))[-1] != "mutation_gate_correction":
            sys.exit(f"{item}: JSON content differs")
        if any(byte > 127 for byte in added_pointer + added_note + new_js[len(old_js) - 3:]):
            sys.exit(f"{item}: non-ASCII byte added")
    print("verify ok", len(RESULTS))


if __name__ == "__main__":
    if len(sys.argv) != 3 or sys.argv[1] not in ("check-artifacts", "apply", "verify"):
        sys.exit(__doc__)
    if sys.argv[1] in ("apply", "verify") and not re.fullmatch(r"\d{4}-\d{2}-\d{2}", sys.argv[2]):
        sys.exit("date must be YYYY-MM-DD")
    check_classification()
    check_blobs()
    {"check-artifacts": check_artifacts, "apply": apply, "verify": verify}[sys.argv[1]](sys.argv[2])

#!/usr/bin/env python3
"""Writes the C03 evidence pack and verdict inputs for full run 44562323424.

Run it from anywhere with `python3 -I`. The inputs are the artifacts and job logs
of the run, downloaded to one directory outside the repository (see INPUT_FILES
for the file names that directory must hold).

  make_results.py --inputs DIR               check everything, then write
                                             results/C03/2026-11-19.json and .md
                                             and README.md line 32, and print the row
  make_results.py --inputs DIR --check       re-render and require byte equality with
                                             the committed files
  make_results.py --inputs DIR --self-test   run the real inputs, then each single-change
                                             case; each must fail at its named check

Nothing is written unless G1 to G6 and G10 pass on the inputs and G8 and G9 pass on the
rendered files. G7 (byte equality with the committed files) runs only with --check. A failure prints
`FAIL G<n>: <reason>` and exits 1. The generator reads no network and holds no
secret. It reads the base commit's files with `git show`.
"""

import argparse
import copy
import hashlib
import io
import json
import pathlib
import re
import subprocess
import sys
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[4]
REPO = "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet"
RUN_ID_NUM = 44562323424
RUN_URL = f"{REPO}/actions/runs/{RUN_ID_NUM}"
RUN_ID = "c03-44562323424-1"
BASE = "a1cf429dc7a57827c2a3d5bb658ae290f5ea9145"
MUTATION_COMMIT = "e85e651af7e291ccdab240c46cdb89edc7ba4c63"
MUTATION_RUN = 44402419383
DATE = "2026-11-19"
JSON_PATH = "results/C03/2026-11-19.json"
MD_PATH = "results/C03/2026-11-19.md"
README_LINE = 32
ARTIFACT_EXPIRY = "2027-02-17T16:06:29Z"

EXPECTED_PATH = "scenarios/C03/expected.yaml"
EXPECTED_SHA = "71ffd90fa84d837bfe173244341388bca0cd0cb2f86bfc963b4f5aaad860b68f"
SCENARIO_PATH = "scenarios/C03/scenario.yaml"
THRESHOLD_US = 45000000
CONFIGS = ("C0", "C1")
REPEATS = [1, 2, 3, 4, 5]

# Input files: key -> (file name in the inputs directory, SHA-256).
INPUT_FILES = {
    "c0cand": ("c03-C0-candidate.zip", "f264f1a0077ccc97650163db6a9809a0442d1d3936185f5016f540c7ed7de11b"),
    "c1cand": ("c03-C1-candidate.zip", "b104889647747d834724353e95db86c5f26f90086d5c59c2969f5cb9a8891c53"),
    "c0arch": ("c03-C0-archive.zip", "ecb35b70d1c0761ff312af7f76abdebcd00617e41eba17a72be5917d8a06b15c"),
    "c1arch": ("c03-C1-archive.zip", "507565ac9b104a296810822685fe39a77ad43a6f258eca50d7ad381b437cd394"),
    "c0pods": ("c03-C0-pods.zip", "5680e14ded810a42610a80d06768f4ffb4fdd0ea3c6c4d4bf98989a9e6b205d6"),
    "c1pods": ("c03-C1-pods.zip", "2e92c81db1194c38eb799bf896305ebf4e60acaef358cf38e4ed33d3fe726e13"),
    "c5": ("c5-summary.zip", "bc72e78858246b9e560587fcf191c067cbf0746d5b09c8b0e8dda4dda56c03f7"),
    "c0log": ("probe.log", "8aed0608dc5d8381ba8a933a1d793375f663ce412bc546e884643f596852d84e"),
    "c1log": ("full-C3.log", "357f8f9dd65d45f98a340c7c99b57112528aefa0e305b251349bbf85239e593c"),
}
ZIP_KEYS = ("c0cand", "c1cand", "c0arch", "c1arch", "c0pods", "c1pods", "c5")
MEMBER_PINS = {
    ("c0cand", "candidate.json"): "2cf06532f472cb2bf0fc8b4f71ba0f23ace3aec0bb69585b153ca6de68540baf",
    ("c1cand", "candidate.json"): "2052d4eed484e12224919fcf151f7dd10b8b2b3d7387ae2026cd6b2733fd6d96",
    ("c0arch", "archive-receipt.json"): "de9e19a729749881311576f5e852be929e69b28307a319941e34b98deb19a630",
    ("c0arch", "archive-manifest.json"): "19acf2f9ca967b1778cd75a474a9224b99e8c0d1f069f61cfa377ffedd450a10",
    ("c1arch", "archive-receipt.json"): "8bf3795a069e022cfff33350834fdfa1773f72f2465686d1f9a1ca6f2373b0a6",
    ("c1arch", "archive-manifest.json"): "388adf58f4d7e6d382bc7dd2bf4d77a7f977bc48300a431883068cb89d832f26",
    ("c0pods", "phase-status.json"): "ee300acc35eb319b47125545f0702706240c99a961cec7ebc853a944ff2c6163",
    ("c1pods", "phase-status.json"): "80efd71bac8aba1431935e868cbee528aa9d91e28df65bba6e7878adf288bc89",
    ("c5", "summary.json"): "55727369b6c9f770c3d3327f978d3f5e268d368319788e183efaa018996b9410",
}
ARTIFACTS = {
    "C0": [("candidate", 14557021737, "c03-C0-candidate-44562323424-1", "c0cand"),
           ("archive", 14558051290, "c03-C0-archive-44562323424-1", "c0arch"),
           ("pods", 14556692046, "c03-C0-pods-44562323424-1", "c0pods")],
    "C1": [("candidate", 14566219424, "c03-C1-candidate-44562323424-1", "c1cand"),
           ("archive", 14568811998, "c03-C1-archive-44562323424-1", "c1arch"),
           ("pods", 14567423531, "c03-C1-pods-44562323424-1", "c1pods")],
}
MUTATION_ARTIFACT = (14477182821, "c5-summary-44402419383-1")
LOGS = {"C0": (135694703105, "probe", "c0log"), "C1": (135754218668, "full-C3", "c1log")}
# Log line numbers of the Terraform plan lines (capacity, location, sku), as the plan cites them.
LOG_LINES = {"C0": (1547, 1556, 1562), "C1": (1382, 1391, 1397)}
JOBS = ("consumer-subject", "consumer-witness", "start-line", "loadgen", "verify")

TITLE = ("# C03: a consumer that kept heartbeating but made no Consume call lost its partitions at the poll limit; "
         "a paused consumer that kept calling Consume kept them")

SUMMARY_PINNED = (
    "In run 44562323424 on Event Hubs Premium, a consumer that processed one message inline for 90000 ms, making no "
    "`Consume` call, lost its partitions in all five repeats of C0: the other group member took over the slow "
    "message's partition 60.55 s to 62.99 s after the slow message started, after the subject's 60000 ms "
    "`max.poll.interval.ms` and not at its 30000 ms `session.timeout.ms`, and the slow message was processed a "
    "second time. In all five repeats of C1, a consumer that paused its partitions for 90000 ms but kept calling "
    "`Consume` kept them, and no message was processed twice. Both deciding hypotheses hold, so the proposed verdict "
    "is `confirmed`; no message was committed without being processed in any of the ten repeats, and the workload "
    "is synthetic."
)
SUMMARY_TEMPLATE = SUMMARY_PINNED.replace("60.55 s to 62.99 s", "{range}")
assert SUMMARY_TEMPLATE != SUMMARY_PINNED

PLAIN_WORDS = (
    "In plain words: a consumer has two separate timers. Heartbeats are sent by a librdkafka background thread, "
    "whether or not the application calls `Consume`, and they keep the member in the group as long as one arrives "
    "within `session.timeout.ms`. `max.poll.interval.ms` is different: a `Consume` call resets it, and when the gap "
    "between calls is longer, librdkafka stops the heartbeats, leaves the group itself, and the partitions move to "
    "another member. So a consumer that blocks in its own processing code for longer than `max.poll.interval.ms` "
    "loses its partitions even though its heartbeats had kept it in the group past the session timeout, as C0 "
    "showed; a consumer that pauses its partitions and keeps calling `Consume`, as C1 did, keeps them."
)
VERDICT_LINE = "Proposed verdict: `confirmed`."

WHAT_TESTED = (
    "Each repeat ran two consumers in one group: a subject that carried the slow message and a witness that ran the "
    "baseline consumer. One message, at the slow-message ordinal 100 fixed in the scenario, took the subject 90000 ms. "
    "In C0 the subject processed it inline, with no `Consume` call for 90000 ms. In C1 the subject paused its "
    "partitions for 90000 ms and kept calling `Consume`. The verifier counted 240 delivered synthetic events per "
    "repeat, in five repeats per configuration."
)
MICROSOFT_SENTENCE = (
    "Microsoft's Event Hubs guidance keeps `max.poll.interval.ms` at its 300000 ms default, says it must be greater "
    "than `session.timeout.ms`, and says to increase it only if processing takes longer than 5 minutes; this run "
    "tested a 60000 ms limit with the same mechanism, not 300000 ms itself."
)
H1_LINE = ("- H1, the poll limit loses partitions (C0): holds. In each of the five repeats the subject received a lost "
           "callback, the witness took the slow message's partition during the slow message {range} after it started, "
           "at or above the 45 s threshold, and a message was processed again.")
H2_LINE = ("- H2, a paused consumer keeps its partitions (C1, K-55): holds. In each of the five repeats the subject "
           "had no lost or revoked callback, the witness received no partition assignment after its initial one, the "
           "slow message was processed once and nothing was processed again.")
H3_LINE = ("- H3, no commit on partitions lost (C0, K-28), published on its own line and not part of the verdict: "
           "holds. No repeat's subject commit-callback rows record a successful commit for the lost partitions between "
           "the lost callback and the subject's next assignment.")
LOSS_LINE = "- Consumer loss, recorded only: 0 messages committed but never processed in each of the ten repeats."
THRESHOLD_PARAGRAPH = (
    "The 45 s threshold is a policy value between the 30 s session timeout and the subject's 60 s poll limit. A "
    "takeover at or above it means the group did not remove the subject at the session timeout, so its heartbeats "
    "continued while it made no `Consume` call."
)
BROKER_SENTENCE = "Broker connection counts are lower bounds, as each repeat's `broker_count.lower_bound` records."
NODE_SENTENCE = "Both consumers of every repeat ran on the same `workload` node and exited 0."
README_ROW_TEMPLATE = (
    "| C03 | Long processing and poll interval | A consumer that kept heartbeating but made no `Consume` call for 90 s "
    "lost its partitions in all five repeats: the other member took over {range} after the slow message started, "
    "after the 60 s poll limit and not at the 30 s session timeout, and the slow message was processed again. A "
    "consumer that paused for 90 s but kept calling `Consume` kept its partitions in all five repeats, with nothing "
    "processed twice. Heartbeats come from a librdkafka background thread and keep a member in the group against "
    "`session.timeout.ms` until `max.poll.interval.ms` is exceeded; `max.poll.interval.ms` is a separate limit that "
    "`Consume` calls reset. The poll limit was scaled to 60000 ms from 300000 ms. Consumer token refresh was not "
    "tested. Synthetic workload. | confirmed | [Report](results/C03/2026-11-19.md) |"
)
README_FRAGMENT = "Heartbeats come from a librdkafka background thread"
H3_INDENT = ("  H3 observes only the time after the application received the lost callback. The window between the "
             "poll-limit expiry and the subject's next Consume call was not observed.")

BLOB = f"{REPO}/blob/{BASE}/"


class Fail(Exception):
    def __init__(self, code, message):
        super().__init__(f"FAIL {code}: {message}")
        self.code = code


def fail(code, message):
    raise Fail(code, message)


def expect(condition, code, message):
    if not condition:
        fail(code, message)


def sha256(data):
    return hashlib.sha256(data).hexdigest()


class Ctx:
    pass


# ----------------------------------------------------------------------------------------------
# Loading


def git(*args, binary=False):
    result = subprocess.run(["git", "-C", str(ROOT), *args], capture_output=True)
    if result.returncode != 0:
        fail("G1", "git " + " ".join(args) + " failed: " + result.stderr.decode("utf-8", "replace").strip())
    return result.stdout if binary else result.stdout.decode("utf-8")


def read_inputs(directory):
    raw = {}
    for key, (name, _pin) in INPUT_FILES.items():
        path = pathlib.Path(directory) / name
        if not path.is_file():
            fail("G1", f"missing input file {name}")
        raw[key] = path.read_bytes()
    return raw


def open_zip(data, key):
    try:
        archive = zipfile.ZipFile(io.BytesIO(data))
    except zipfile.BadZipFile:
        fail("G1", f"{key} is not a ZIP file")
    return {name: archive.read(name) for name in archive.namelist() if not name.endswith("/")}


def yaml_block(text, path):
    """Reads one scalar of expected.yaml by key path: a plain value, or a folded `>-` block.

    A folded block is the indented lines that follow, joined by single spaces, which is
    how YAML folds them. Only plain nested mappings are handled."""
    lines = text.splitlines()
    stack = []
    for index, line in enumerate(lines):
        match = re.match(r"^( *)([A-Za-z_][A-Za-z0-9_./-]*):(?: (.*))?$", line)
        if not match:
            continue
        indent = len(match.group(1))
        while stack and stack[-1][0] >= indent:
            stack.pop()
        stack.append((indent, match.group(2)))
        if [key for _, key in stack] != path:
            continue
        value = match.group(3)
        if value == ">-":
            parts = []
            for follow in lines[index + 1:]:
                if follow.strip() == "":
                    break
                if len(follow) - len(follow.lstrip(" ")) <= indent:
                    break
                parts.append(follow.strip())
            return " ".join(parts)
        if value:
            return value
    fail("G1", "expected.yaml has no scalar at " + ".".join(path))


def new_context(raw):
    ctx = Ctx()
    ctx.raw = raw
    ctx.members = {key: open_zip(raw[key], key) for key in ZIP_KEYS}
    ctx.expected_text = git("show", f"{BASE}:{EXPECTED_PATH}", binary=True)
    ctx.working_expected = (ROOT / EXPECTED_PATH).read_bytes()
    return ctx


# ----------------------------------------------------------------------------------------------
# Checks


def check_g1(ctx):
    for key, (name, pin) in INPUT_FILES.items():
        expect(sha256(ctx.raw[key]) == pin, "G1", f"{name} differs from its pinned SHA-256")
    for (key, member), pin in MEMBER_PINS.items():
        expect(member in ctx.members[key], "G1", f"{key} has no member {member}")
        expect(sha256(ctx.members[key][member]) == pin, "G1", f"member {member} of {key} differs from its pin")
    expect(sha256(ctx.expected_text) == EXPECTED_SHA, "G1", "expected.yaml at the base commit differs from its pin")
    expect(sha256(ctx.working_expected) == EXPECTED_SHA, "G1", "expected.yaml in the working tree differs from its pin")


def strict_json(data, label):
    def no_constant(name):
        raise ValueError(name)

    try:
        return json.loads(data.decode("utf-8"), parse_constant=no_constant)
    except (ValueError, UnicodeDecodeError) as error:
        fail("G1", f"{label} is not strict JSON: {error}")


def job_dir(phase, repeat, job):
    p = phase.lower()
    return f"{RUN_ID}-{p}-judge" if job == "judge" else f"{RUN_ID}-{p}-r{repeat}-{job}"


def job_members(ctx, phase, repeat, job):
    """The pod log and pods.json member paths of one Job folder inside the pods artifact."""
    members = ctx.members["c0pods" if phase == "C0" else "c1pods"]
    prefix = job_dir(phase, repeat, job) + "/"
    names = sorted(n for n in members if n.startswith(prefix))
    logs = [n for n in names if n.endswith(".log")]
    pods = [n for n in names if n == prefix + "pods.json"]
    expect(len(logs) == 1 and len(pods) == 1, "G1", f"{prefix} does not hold exactly one pod log and one pods.json")
    return logs[0], pods[0]


def parse(ctx):
    ctx.cand = {
        "C0": strict_json(ctx.members["c0cand"]["candidate.json"], "C0 candidate"),
        "C1": strict_json(ctx.members["c1cand"]["candidate.json"], "C1 candidate"),
    }
    ctx.receipt = {
        "C0": strict_json(ctx.members["c0arch"]["archive-receipt.json"], "C0 archive receipt"),
        "C1": strict_json(ctx.members["c1arch"]["archive-receipt.json"], "C1 archive receipt"),
    }
    ctx.phase = {
        "C0": strict_json(ctx.members["c0pods"]["phase-status.json"], "C0 phase status"),
        "C1": strict_json(ctx.members["c1pods"]["phase-status.json"], "C1 phase status"),
    }
    ctx.summary = strict_json(ctx.members["c5"]["summary.json"], "C5 summary")
    ctx.pods = {}
    ctx.jobfiles = {}
    for phase in CONFIGS:
        for repeat in REPEATS:
            for job in JOBS:
                ctx.jobfiles[(phase, repeat, job)] = job_members(ctx, phase, repeat, job)
            for role in ("consumer-subject", "consumer-witness"):
                _log, pods = ctx.jobfiles[(phase, repeat, role)]
                ctx.pods[(phase, repeat, role)] = strict_json(
                    ctx.members["c0pods" if phase == "C0" else "c1pods"][pods], f"pods.json {phase} r{repeat} {role}")
        ctx.jobfiles[(phase, None, "judge")] = job_members(ctx, phase, None, "judge")


def check_g2(ctx):
    for phase in CONFIGS:
        c = ctx.cand[phase]
        expect(c.get("scenario_id") == "C03" and c.get("configuration_id") == phase and c.get("phase") == phase
               and c.get("run_id") == RUN_ID, "G2", f"{phase} candidate identity")
        expect(c.get("source_commit") == BASE and c.get("synthetic") is True, "G2", f"{phase} candidate source commit or synthetic flag")
        expect(c.get("status") == "complete_valid" and c.get("reasons") == [], "G2", f"{phase} candidate status or reasons")
        consumer = c.get("consumer", {})
        expect(consumer.get("status") == "complete_valid" and consumer.get("reasons") == [], "G2", f"{phase} root consumer status or reasons")
        repeats = c.get("repeats")
        expect(isinstance(repeats, list) and [r.get("repeat") for r in repeats] == REPEATS, "G2", f"{phase} repeats are not 1 to 5")
        for r in repeats:
            expect(r.get("status") == "complete_valid" and r.get("reasons") == [], "G2", f"{phase} repeat {r.get('repeat')} status or reasons")
            expect(r["consumer"].get("status") == "complete_valid" and r["consumer"].get("reasons") == [], "G2",
                   f"{phase} repeat {r['repeat']} consumer status or reasons")
    expect(ctx.cand["C0"]["versions"] == ctx.cand["C1"]["versions"], "G2", "the two candidates' versions differ")


def holds_rule(items, holds_when, contradicted_when):
    if holds_when(items):
        return "holds"
    return "contradicted" if contradicted_when(items) else "inconclusive"


def recompute(ctx):
    """Each rule of scenarios/C03/expected.yaml, from the candidates, as written there."""
    out = {}
    c0 = ctx.cand["C0"]["repeats"]
    c1 = ctx.cand["C1"]["repeats"]
    all_valid = lambda reps: len(reps) == 5 and all(r["status"] == "complete_valid" and r["consumer"]["status"] == "complete_valid" for r in reps)
    m = lambda r: r["consumer"]["membership"]

    def h1_holds(reps):
        return all_valid(reps) and all(
            m(r)["subject_lost_rows"] >= 1 and m(r)["witness_assigned_slow_partition_during_slow"] is True
            and m(r)["witness_takeover_microseconds"] is not None and m(r)["witness_takeover_microseconds"] >= THRESHOLD_US
            and r["consumer"]["counts"]["processed_again"] >= 1 for r in reps)

    def h1_contradicted(reps):
        return all_valid(reps) and any(
            (m(r)["subject_lost_rows"] == 0 and m(r)["subject_revoked_rows"] == 0)
            or (m(r)["witness_takeover_microseconds"] is not None and m(r)["witness_takeover_microseconds"] < THRESHOLD_US)
            for r in reps)

    out["H1"] = holds_rule(c0, h1_holds, h1_contradicted)
    h3_holds = h1_holds(c0) and all(m(r)["subject_commit_entries_after_lost"] == 0 for r in c0)
    h3_contra = all_valid(c0) and any(m(r)["subject_lost_rows"] >= 1 and m(r)["subject_commit_entries_after_lost"] >= 1 for r in c0)
    out["H3"] = "holds" if h3_holds else "contradicted" if h3_contra else "inconclusive"
    out["H2"] = holds_rule(
        c1,
        lambda reps: all_valid(reps) and all(
            m(r)["subject_lost_rows"] == 0 and m(r)["subject_revoked_rows"] == 0 and m(r)["witness_assigned_rows"] == 0
            and m(r)["slow_processed_rows"] == 1 and r["consumer"]["counts"]["processed_again"] == 0 for r in reps),
        lambda reps: all_valid(reps) and any(m(r)["subject_lost_rows"] >= 1 for r in reps))
    out["loss"] = {
        "C0": [r["consumer"]["counts"]["committed_never_processed"] for r in c0],
        "C1": [r["consumer"]["counts"]["committed_never_processed"] for r in c1],
    }
    if out["H1"] == "holds" and out["H2"] == "holds":
        out["verdict"] = "confirmed"
    elif out["H1"] == "contradicted" or out["H2"] == "contradicted":
        out["verdict"] = "contradicted"
    else:
        out["verdict"] = "no_verdict"
    return out


def pod_facts(pods):
    items = pods.get("items", [])
    node = {i["spec"].get("nodeName") for i in items}
    codes = []
    for i in items:
        for status in i.get("status", {}).get("containerStatuses", []):
            terminated = status.get("state", {}).get("terminated")
            codes.append(terminated.get("exitCode") if terminated else None)
    return items, node, codes


def check_g3(ctx):
    got = recompute(ctx)
    expect(got["H1"] == "holds", "G3", f"H1 recomputes to {got['H1']}, not holds")
    expect(got["H3"] == "holds", "G3", f"H3 recomputes to {got['H3']}, not holds")
    expect(got["H2"] == "holds", "G3", f"H2 recomputes to {got['H2']}, not holds")
    expect(got["loss"] == {"C0": [0] * 5, "C1": [0] * 5}, "G3", f"consumer loss is {got['loss']}, not all zero")
    expect(got["verdict"] == "confirmed", "G3", f"verdict recomputes to {got['verdict']}, not confirmed")
    for r in ctx.cand["C0"]["repeats"]:
        mem = r["consumer"]["membership"]
        expect(mem["slow_processed_rows"] == 2 and r["consumer"]["counts"]["processed_again"] == 1 and mem["subject_revoked_rows"] == 0,
               "G3", f"C0 repeat {r['repeat']}: the slow message is not the one processed again, or a revoked row exists")
    for phase in CONFIGS:
        for repeat in REPEATS:
            nodes = []
            for role in ("consumer-subject", "consumer-witness"):
                items, node, codes = pod_facts(ctx.pods[(phase, repeat, role)])
                expect(len(items) == 1 and len(node) == 1 and None not in node and codes and all(c == 0 for c in codes),
                       "G3", f"{phase} repeat {repeat} {role}: not one pod on one node with every container exited 0")
                nodes.append(next(iter(node)))
            expect(nodes[0] == nodes[1], "G3", f"{phase} repeat {repeat}: subject and witness ran on different nodes")


def check_g4(ctx):
    for phase in CONFIGS:
        c = ctx.cand[phase]
        for key in c["counts"]:
            expect(c["counts"][key] == sum(r["counts"][key] for r in c["repeats"]), "G4", f"{phase} root counts.{key} is not the sum over repeats")
        for key in c["consumer"]["counts"]:
            expect(c["consumer"]["counts"][key] == sum(r["consumer"]["counts"][key] for r in c["repeats"]), "G4",
                   f"{phase} root consumer.counts.{key} is not the sum over repeats")


def check_g5(ctx):
    for phase in CONFIGS:
        rc = ctx.receipt[phase]
        manifest_pin = MEMBER_PINS[("c0arch" if phase == "C0" else "c1arch", "archive-manifest.json")]
        ident = rc.get("identity", {})
        expect(rc.get("archive_complete") is True and rc.get("gaps") == [] and rc.get("kind") == "run_evidence_archive_receipt",
               "G5", f"{phase} archive receipt is not complete with no gaps")
        expect(ident.get("github_run_id") == str(RUN_ID_NUM) and ident.get("github_run_attempt") == 1 and ident.get("phase") == phase
               and ident.get("run_id") == RUN_ID and ident.get("source_commit") == BASE, "G5", f"{phase} archive receipt identity")
        expect(rc.get("manifest", {}).get("sha256") == manifest_pin, "G5", f"{phase} receipt manifest hash differs from the manifest member")


def check_g6(ctx):
    for phase in CONFIGS:
        ps = ctx.phase[phase]
        expect(ps.get("phase") == phase and ps.get("safety_stop") is False, "G6", f"{phase} phase status phase or safety_stop")
        cand = ps.get("candidate", {})
        expect(cand.get("status") == "complete_valid" and cand.get("repeats") == [[n, "complete_valid"] for n in REPEATS], "G6",
               f"{phase} phase status candidate")
        for mode in ps.get("modes", []):
            allowed = "ready" if mode.get("mode") == "consumer-readiness" else "complete"
            expect(mode.get("outcome") == allowed, "G6", f"{phase} phase status Job {mode.get('job')} outcome {mode.get('outcome')}")


def check_g10(ctx):
    s = ctx.summary
    expect(s.get("commit") == MUTATION_COMMIT and s.get("measurement_complete") is True and s.get("break_passed") is True,
           "G10", "mutation summary commit, measurement_complete or break_passed")


def check_g8(md):
    expect(md.isascii(), "G8", "the rendered report has a byte outside ASCII")


def check_g9(ctx, md, row):
    expect(md.startswith(TITLE + "\n\n" + SUMMARY_PINNED + "\n\n" + PLAIN_WORDS + "\n\n" + VERDICT_LINE + "\n"), "G9",
           "the report opening differs from the pinned title, summary, plain-words paragraph and verdict line")
    expect(H3_INDENT in md, "G9", "the H3 indented line is missing")
    reason = yaml_block(ctx.expected_text.decode("utf-8"), ["not_tested", "K-47", "reason"])
    expect(reason in md, "G9", "the K-47 reason is missing")
    expect(README_FRAGMENT in row, "G9", "the README row lacks the heartbeat sentence")


# ----------------------------------------------------------------------------------------------
# Publication


def seconds(us):
    cents = us // 10000
    return f"{cents // 100}.{cents % 100:02d}"


def takeover_range(ctx):
    values = [r["consumer"]["membership"]["witness_takeover_microseconds"] for r in ctx.cand["C0"]["repeats"]]
    return f"{seconds(min(values))} s to {seconds(max(values))} s"


def build_publication(ctx):
    text = ctx.expected_text.decode("utf-8")
    cands = ctx.cand
    inputs = []
    for phase in CONFIGS:
        for kind, artifact_id, name, key in ARTIFACTS[phase]:
            inputs.append({"name": f"{phase} {kind} artifact ZIP", "source": f"artifact {artifact_id}, {name}", "sha256": INPUT_FILES[key][1]})
            for (k, member), pin in MEMBER_PINS.items():
                if k == key:
                    inputs.append({"name": f"{phase} {kind} member {member}", "source": f"member {member} of artifact {artifact_id}", "sha256": pin})
    for phase in CONFIGS:
        job_id, job_key, key = LOGS[phase]
        inputs.append({"name": f"{phase} job log", "source": f"job {job_id} ({job_key})", "sha256": INPUT_FILES[key][1]})
    inputs.append({"name": "mutation summary artifact ZIP", "source": f"artifact {MUTATION_ARTIFACT[0]}, {MUTATION_ARTIFACT[1]}", "sha256": INPUT_FILES["c5"][1]})
    inputs.append({"name": "mutation summary.json", "source": f"member summary.json of artifact {MUTATION_ARTIFACT[0]}", "sha256": MEMBER_PINS[("c5", "summary.json")]})
    inputs.append({"name": EXPECTED_PATH, "source": f"git show {BASE}:{EXPECTED_PATH} (the working tree equals it)", "sha256": EXPECTED_SHA})
    nodes = {
        phase: [next(iter(pod_facts(ctx.pods[(phase, repeat, "consumer-subject")])[1])) for repeat in REPEATS]
        for phase in CONFIGS
    }
    publication = {
        "item": "C03",
        "workload": "synthetic",
        "run": {
            "workflow_run_id": RUN_ID_NUM, "attempt": 1, "event": "workflow_dispatch", "ref": "main", "head_sha": BASE,
            "created_at": "2026-11-19T16:06:27Z", "conclusion": "success", "run_id": RUN_ID,
        },
        "expected_outcome": {"path": EXPECTED_PATH, "sha256": EXPECTED_SHA},
        "proposed_verdict": "confirmed",
        "rule": "scenarios/C03/expected.yaml verdict.confirmed_when: H1 holds and H2 holds",
        "hypotheses": {
            "H1_poll_limit_loses_partitions": {"configuration": "C0", "result": "holds"},
            "H2_pause_keeps_partitions": {"configuration": "C1", "result": "holds"},
            "H3_no_commit_on_lost": {"configuration": "C0", "result": "holds", "separate_line": True},
        },
        "consumer_loss": {"C0": [0] * 5, "C1": [0] * 5, "mode": "recorded_only"},
        "c0_takeover_microseconds": [r["consumer"]["membership"]["witness_takeover_microseconds"] for r in cands["C0"]["repeats"]],
        "takeover_threshold_microseconds": THRESHOLD_US,
        "scaled_limit": yaml_block(text, ["assessment_rules", "scaled_limit"]),
        "not_tested": {"K-47": yaml_block(text, ["not_tested", "K-47", "reason"])},
        "archives": {
            phase: {"archive_complete": ctx.receipt[phase]["archive_complete"], "gaps": ctx.receipt[phase]["gaps"],
                    "manifest_sha256": ctx.receipt[phase]["manifest"]["sha256"]} for phase in CONFIGS
        },
        "consumer_nodes": nodes,
        "broker_connection_counts": {
            phase: [{"repeat": r["repeat"], **{k: r["broker_count"][k] for k in ("role", "maximum", "first_post_drain", "lower_bound")}}
                    for r in cands[phase]["repeats"]] for phase in CONFIGS
        },
        "environment": {
            "namespace_sku": "Premium", "processing_units": 1, "region": "uksouth", "node_size": "Standard_D4ds_v5",
            "node_size_source": "inferred from infra/cluster/aks.tf lines 35, 44 and 51 at the base commit; not captured by the run",
        },
        "versions": cands["C0"]["versions"],
        "mutation_acceptance": {
            "workflow_run_id": MUTATION_RUN, "commit": ctx.summary["commit"], "measurement_complete": ctx.summary["measurement_complete"],
            "break_passed": ctx.summary["break_passed"], "summary_sha256": MEMBER_PINS[("c5", "summary.json")],
            "relevance_check_to_run_commit": "not_applicable",
        },
        "inputs": inputs,
    }
    return {"publication": publication, "candidates": {"C0": cands["C0"], "C1": cands["C1"]}}


# ----------------------------------------------------------------------------------------------
# Rendering


def table(headers, rows):
    lines = ["| " + " | ".join(headers) + " |", "|" + "|".join("---" for _ in headers) + "|"]
    for row in rows:
        lines.append("| " + " | ".join(str(cell) for cell in row) + " |")
    return "\n".join(lines)


def yes(value):
    return "yes" if value else "no"


def render_row(ctx):
    return README_ROW_TEMPLATE.format(range=takeover_range(ctx))


def render_md(ctx, document):
    publication = document["publication"]
    cands = ctx.cand
    text = ctx.expected_text.decode("utf-8")
    rng = takeover_range(ctx)
    out = [TITLE, "", SUMMARY_TEMPLATE.format(range=rng), "", PLAIN_WORDS, "", VERDICT_LINE, ""]

    out += ["## What was tested", "", WHAT_TESTED, "", "Scaled limit (from the expected outcome):", ""]
    out += ["> " + publication["scaled_limit"], "", MICROSOFT_SENTENCE, ""]

    out += ["## Results by repeat", "", "**C0, inline slow message**", ""]
    rows = []
    for r in cands["C0"]["repeats"]:
        mem, cnt = r["consumer"]["membership"], r["consumer"]["counts"]
        rows.append([r["repeat"], r["status"], mem["subject_lost_rows"], mem["subject_revoked_rows"],
                     yes(mem["witness_assigned_slow_partition_during_slow"]), seconds(mem["witness_takeover_microseconds"]),
                     mem["slow_processed_rows"], cnt["processed_again"], cnt["committed_never_processed"], mem["subject_commit_entries_after_lost"]])
    out += [table(["Repeat", "Status", "Subject lost rows", "Subject revoked rows", "Witness took slow partition during slow message",
                   "Takeover (s)", "Slow message processed rows", "Processed again", "Committed, never processed",
                   "Subject commit entries after lost"], rows), "", "**C1, paused slow message**", ""]
    rows = []
    for r in cands["C1"]["repeats"]:
        mem, cnt = r["consumer"]["membership"], r["consumer"]["counts"]
        rows.append([r["repeat"], r["status"], mem["subject_lost_rows"], mem["subject_revoked_rows"], mem["witness_assigned_rows"],
                     mem["slow_processed_rows"], cnt["processed_again"], cnt["committed_never_processed"]])
    out += [table(["Repeat", "Status", "Subject lost rows", "Subject revoked rows", "Witness assigned rows",
                   "Slow message processed rows", "Processed again", "Committed, never processed"], rows), ""]

    out += ["## Hypotheses", "", H1_LINE.format(range=rng), H2_LINE, H3_LINE, H3_INDENT, LOSS_LINE, "", THRESHOLD_PARAGRAPH, ""]

    out += ["## Producer side and broker connections", ""]
    rows = []
    for phase in CONFIGS:
        for r in cands[phase]["repeats"]:
            c, b = r["counts"], r["broker_count"]
            rows.append([phase, r["repeat"], c["ok"], c["loss"], c["phantom"], c["reported_failure"], c["duplicate"], c["reorder"],
                         b["maximum"], b["first_post_drain"]])
    out += [table(["Configuration", "Repeat", "ok", "loss", "phantom", "reported_failure", "duplicate", "reorder",
                   "Broker connections (maximum)", "Broker connections (first after drain)"], rows), "", BROKER_SENTENCE, ""]

    out += ["## Run validity and evidence", ""]
    out += [
        f"- Run [{RUN_ID_NUM}]({RUN_URL}), attempt 1, event `workflow_dispatch`, ref `main`, head `{BASE}`, created 2026-11-19T16:06:27Z, conclusion `success`.",
        "- Both candidates are `complete_valid` with no reasons.",
        "- Both phase-status files report every Job `complete` (`consumer-readiness` `ready`) and `safety_stop` false.",
    ]
    for phase in CONFIGS:
        rc = ctx.receipt[phase]
        out += [f"- {phase} archive receipt: `archive_complete` true, no gaps, manifest SHA-256 `{rc['manifest']['sha256']}`."]
    out += ["", "Artifacts of the run (each ZIP's SHA-256 is in the JSON `inputs` list):", ""]
    for phase in CONFIGS:
        for kind, artifact_id, name, _key in ARTIFACTS[phase]:
            out += [f"- {phase} {kind}: [{artifact_id}]({RUN_URL}/artifacts/{artifact_id}), `{name}`."]
    out += ["", "Per-repeat pod evidence: the member paths inside each pods artifact of the pod log and `pods.json` of each Job.", ""]
    rows = []
    for phase in CONFIGS:
        for repeat in REPEATS + [None]:
            for job in (JOBS if repeat else ("judge",)):
                log, pods = ctx.jobfiles[(phase, repeat, job)]
                rows.append([phase, repeat if repeat else "all", job, f"`{log}`", f"`{pods}`"])
    out += [table(["Phase", "Repeat", "Job", "Pod log member", "pods.json member"], rows), ""]
    rows = []
    for phase in CONFIGS:
        for repeat in REPEATS:
            _items, node, _codes = pod_facts(ctx.pods[(phase, repeat, "consumer-subject")])
            rows.append([phase, repeat, f"`{next(iter(node))}`"])
    out += [table(["Phase", "Repeat", "Node of the subject and witness pods"], rows), "", NODE_SENTENCE, "",
            f"Artifacts expire {ARTIFACT_EXPIRY}.", ""]

    out += ["## Environment and versions", ""]
    v = publication["versions"]
    out += [
        "- Event Hubs namespace SKU Premium, 1 processing unit and region uksouth, from the Terraform plan lines of the job logs: "
        f"`{LOGS['C0'][1]}` log (C0) lines {LOG_LINES['C0'][2]}, {LOG_LINES['C0'][0]} and {LOG_LINES['C0'][1]}; "
        f"`{LOGS['C1'][1]}` log (C1) lines {LOG_LINES['C1'][2]}, {LOG_LINES['C1'][0]} and {LOG_LINES['C1'][1]} "
        "(`sku`, `capacity`, `location`; the lines carry colour escapes and are cited by number, not quoted).",
        "- AKS node size Standard_D4ds_v5 is inferred from `infra/cluster/aks.tf` lines 35, 44 and 51 at the base commit, not captured from the run.",
        f"- Versions: Confluent.Kafka `{v['confluent_kafka']}`, librdkafka `{v['librdkafka']}`, .NET `{v['dotnet']}`, verifier `{v['verifier']}`, workload generator `{v['workload_generator']}`.",
        f"- Source commit: `{BASE}`.",
    ]
    for phase in CONFIGS:
        t = cands[phase]["timings"]
        out += [f"- {phase}: worker started {t['worker_started_utc']}, judge completed {t['judge_completed_utc']}."]
    out += [""]

    mutation = publication["mutation_acceptance"]
    out += [
        "## Mutation acceptance", "",
        f"C5 mutation run [{mutation['workflow_run_id']}]({REPO}/actions/runs/{mutation['workflow_run_id']}) on `{mutation['commit']}` "
        f"reports `measurement_complete` {str(mutation['measurement_complete']).lower()} and `break_passed` {str(mutation['break_passed']).lower()} "
        f"(summary SHA-256 `{mutation['summary_sha256']}`). That commit is an ancestor of `{BASE}`, and "
        f"`c5_runtime.py check --base {MUTATION_COMMIT} --head {BASE}` reports `not_applicable`, so no mutation input changed between them "
        "(docs/agents/factory.md \"Evidence pack\").", "",
    ]

    out += ["## Not tested", "",
            "- Consumer token refresh only inside `Consume` (K-47): not tested. " + publication["not_tested"]["K-47"],
            "- The 300000 ms baseline poll limit and a 6-minute slow message: the run scaled both, as stated above.",
            "- Whether a commit can happen between the poll-limit expiry and the subject's next `Consume` call: not observed (H3).", ""]

    lib = "https://github.com/confluentinc/librdkafka/blob/v2.16.0/"
    conf = "https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/"
    out += ["## Sources", "",
            f"- Run [{RUN_ID_NUM}]({RUN_URL}), its artifacts and job logs, as listed above and in the JSON `inputs`.",
            f"- Scenario definition and expected outcome as approved: [scenarios/C03/scenario.yaml]({BLOB}{SCENARIO_PATH}) (the slow-message ordinal 100, the 90000 ms slow message, the inline and pause modes and the 60000 ms poll limit) and [scenarios/C03/expected.yaml]({BLOB}{EXPECTED_PATH}) (SHA-256 `{EXPECTED_SHA}`; the hypotheses, the 45 s threshold, the scaled limit and the K-47 reason), both at `{BASE}`.",
            f"- librdkafka v2.16.0 [`src/rdkafka_cgrp.c`]({lib}src/rdkafka_cgrp.c): lines 7269, 7362 and 7388 (the group serve loop reaches the join-state handler), 6808 to 6816 (heartbeats in the steady state), 3959 to 3962 (no heartbeat once `max.poll.interval.ms` is exceeded) and 5741 to 5806 (the poll-limit callback leaves the group and revokes the partitions as lost).",
            f"- librdkafka v2.16.0 [`src/rdkafka.c`]({lib}src/rdkafka.c): lines 2434 to 2438, 2486 and 3003 (the internal thread that serves the group), and 5082, 5105, 5183 and 5192 (`rd_kafka_consumer_poll` reaches `rd_kafka_app_polled`, which resets the poll timer).",
            f"- librdkafka v2.16.0 [`src/rdkafka_int.h`]({lib}src/rdkafka_int.h) lines 1333 to 1352 and 1405 to 1410 (the poll timer), [`CONFIGURATION.md`]({lib}CONFIGURATION.md) lines 131 and 137, and [`INTRODUCTION.md`]({lib}INTRODUCTION.md) line 2529 (KIP-62 background heartbeats).",
            f"- Confluent.Kafka v2.16.0 [`Consumer.cs`]({conf}src/Confluent.Kafka/Consumer.cs) line 911 and [`SafeKafkaHandle.cs`]({conf}src/Confluent.Kafka/Impl/SafeKafkaHandle.cs) line 764 (`Consume` calls `consumer_poll`).",
            "- Microsoft, [Apache Kafka client configurations for Azure Event Hubs](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations), librdkafka \"Consumer only\" table, read 2026-11-19.",
            f"- This repository at `{BASE}`: [`BaselineSettingsCatalog.cs`]({BLOB}src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs) line 145 (`group.protocol` `classic`), [`aks.tf`]({BLOB}infra/cluster/aks.tf) lines 35, 44 and 51, and [verifier-contract.md]({BLOB}docs/operations/verifier-contract.md) line 128 (the consumer `membership` fields).",
            f"- C5 mutation run [{MUTATION_RUN}]({REPO}/actions/runs/{MUTATION_RUN}) and its `summary.json`, as in Mutation acceptance.",
            "- Generator and receipt: [make_results.py](../../docs/operations/evidence/issue-511-c03-verdict/make_results.py) and [receipt.md](../../docs/operations/evidence/issue-511-c03-verdict/receipt.md).",
            ""]
    return "\n".join(out).rstrip("\n") + "\n"


# ----------------------------------------------------------------------------------------------
# Pipeline


def pipeline(raw, mode="render", hooks=None, committed=None):
    hooks = hooks or {}

    def hook(stage, *args):
        if stage in hooks:
            hooks[stage](*args)

    ctx = new_context(raw)
    hook("members", ctx)
    hook("working", ctx)
    check_g1(ctx)
    parse(ctx)
    hook("parsed", ctx)
    check_g2(ctx)
    check_g3(ctx)
    check_g4(ctx)
    check_g5(ctx)
    check_g6(ctx)
    check_g10(ctx)
    document = build_publication(ctx)
    md_box = [render_md(ctx, document)]
    hook("md", md_box)
    md = md_box[0]
    json_text = json.dumps(document, indent=2, ensure_ascii=True) + "\n"
    row = render_row(ctx)
    check_g8(md)
    check_g9(ctx, md, row)
    result = {"md": md, "json": json_text, "row": row}
    if mode == "check":
        files = committed if committed is not None else read_committed()
        hook("committed", files)
        expect(files["md"] == md, "G7", f"{MD_PATH} differs from the re-rendered report")
        expect(files["json"] == json_text, "G7", f"{JSON_PATH} differs from the re-rendered JSON")
        expect(files["readme"] == row, "G7", f"README.md line {README_LINE} differs from the re-rendered row")
    return result


def read_committed():
    readme = (ROOT / "README.md").read_text(encoding="utf-8").split("\n")
    return {
        "md": (ROOT / MD_PATH).read_text(encoding="utf-8") if (ROOT / MD_PATH).exists() else "",
        "json": (ROOT / JSON_PATH).read_text(encoding="utf-8") if (ROOT / JSON_PATH).exists() else "",
        "readme": readme[README_LINE - 1],
    }


def write_outputs(result):
    readme_path = ROOT / "README.md"
    lines = readme_path.read_text(encoding="utf-8").split("\n")
    expect(lines[README_LINE - 1].startswith("| C03 |"), "G1", f"README.md line {README_LINE} is not the C03 row")
    (ROOT / "results" / "C03").mkdir(parents=True, exist_ok=True)
    (ROOT / MD_PATH).write_text(result["md"], encoding="utf-8", newline="\n")
    (ROOT / JSON_PATH).write_text(result["json"], encoding="utf-8", newline="\n")
    lines[README_LINE - 1] = result["row"]
    readme_path.write_text("\n".join(lines), encoding="utf-8", newline="\n")


# ----------------------------------------------------------------------------------------------
# Self-test


def flip(data, index=10):
    buffer = bytearray(data)
    buffer[index] ^= 1
    return bytes(buffer)


def case_table():
    def parsed(fn):
        return {"parsed": fn}

    def rep(ctx, phase, number):
        return ctx.cand[phase]["repeats"][number - 1]

    def mem(ctx, phase, number):
        return rep(ctx, phase, number)["consumer"]["membership"]

    def set_(target, key, value):
        target[key] = value

    def node_split(ctx):
        ctx.pods[("C1", 5, "consumer-witness")]["items"][0]["spec"]["nodeName"] = "another-node"

    def md_replace(old, new):
        def apply(box):
            expect(old in box[0], "SELFTEST", f"case text not found: {old[:40]}")
            box[0] = box[0].replace(old, new, 1)

        return {"md": apply}

    def committed_edit(files):
        expect("| 1 | complete_valid |" in files["md"], "SELFTEST", "committed report has no repeat 1 row")
        files["md"] = files["md"].replace("| 1 | complete_valid |", "| 1 | complete_invalid |", 1)

    return [
        ("hash_c0_candidate", "G1", {"members": lambda ctx: ctx.members["c0cand"].__setitem__("candidate.json", flip(ctx.members["c0cand"]["candidate.json"]))}),
        ("expected_yaml_edit", "G1", {"working": lambda ctx: setattr(ctx, "working_expected", flip(ctx.working_expected))}),
        ("c0_repeat_status", "G2", parsed(lambda ctx: set_(rep(ctx, "C0", 3)["consumer"], "status", "incomplete"))),
        ("versions_differ", "G2", parsed(lambda ctx: set_(ctx.cand["C1"]["versions"], "librdkafka", "2.15.0"))),
        ("h1_takeover_at_threshold", "G9", parsed(lambda ctx: set_(mem(ctx, "C0", 4), "witness_takeover_microseconds", 45000000))),
        ("h1_takeover_below", "G3", parsed(lambda ctx: set_(mem(ctx, "C0", 4), "witness_takeover_microseconds", 44999999))),
        ("h1_no_lost", "G3", parsed(lambda ctx: set_(mem(ctx, "C0", 2), "subject_lost_rows", 0))),
        ("h1_no_reprocess", "G3", parsed(lambda ctx: set_(rep(ctx, "C0", 5)["consumer"]["counts"], "processed_again", 0))),
        ("h3_commit_after_lost", "G3", parsed(lambda ctx: set_(mem(ctx, "C0", 1), "subject_commit_entries_after_lost", 1))),
        ("h2_lost", "G3", parsed(lambda ctx: set_(mem(ctx, "C1", 1), "subject_lost_rows", 1))),
        ("h2_witness_assigned", "G3", parsed(lambda ctx: set_(mem(ctx, "C1", 3), "witness_assigned_rows", 1))),
        ("loss_nonzero", "G3", parsed(lambda ctx: set_(rep(ctx, "C1", 2)["consumer"]["counts"], "committed_never_processed", 1))),
        ("slow_rows", "G3", parsed(lambda ctx: set_(mem(ctx, "C0", 3), "slow_processed_rows", 3))),
        ("node_split", "G3", parsed(node_split)),
        ("root_consumer_sum", "G4", parsed(lambda ctx: set_(ctx.cand["C0"]["consumer"]["counts"], "processed_again", 4))),
        ("root_producer_sum", "G4", parsed(lambda ctx: set_(ctx.cand["C1"]["counts"], "ok", 1199))),
        ("archive_gap", "G5", parsed(lambda ctx: set_(ctx.receipt["C0"], "gaps", ["x"]))),
        ("archive_phase", "G5", parsed(lambda ctx: set_(ctx.receipt["C1"]["identity"], "phase", "C0"))),
        ("phase_safety_stop", "G6", parsed(lambda ctx: set_(ctx.phase["C0"], "safety_stop", True))),
        ("mutation_incomplete", "G10", parsed(lambda ctx: set_(ctx.summary, "measurement_complete", False))),
        ("non_ascii", "G8", md_replace("## Sources", "## Sources " + chr(0x2013))),
        ("missing_plain_words", "G9", md_replace(PLAIN_WORDS + "\n\n", "")),
        ("report_edit", "G7", {"committed": committed_edit}),
    ]


def self_test(raw):
    problems = 0
    try:
        pipeline(copy.deepcopy(raw))
        print("PASS real_inputs")
    except Fail as error:
        print(f"FAIL real_inputs: {error}")
        return 1
    for name, code, hooks in case_table():
        mode = "check" if code == "G7" else "render"
        try:
            pipeline(copy.deepcopy(raw), mode=mode, hooks=hooks)
        except Fail as error:
            if error.code == code:
                print(f"PASS {name}")
            else:
                print(f"FAIL {name}: failed at {error.code}, expected {code}: {error}")
                problems += 1
        else:
            print(f"FAIL {name}: passed unexpectedly, expected {code}")
            problems += 1
    return 1 if problems else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--inputs", required=True, help="directory holding the downloaded artifacts and job logs")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--check", action="store_true")
    group.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    try:
        raw = read_inputs(args.inputs)
        if args.self_test:
            return self_test(raw)
        if args.check:
            pipeline(raw, mode="check")
            print("PASS check")
            return 0
        result = pipeline(raw)
        write_outputs(result)
        print(result["row"])
        return 0
    except Fail as error:
        print(error)
        return 1


if __name__ == "__main__":
    sys.exit(main())

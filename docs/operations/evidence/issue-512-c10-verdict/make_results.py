#!/usr/bin/env python3
"""Writes the C10 evidence pack and verdict inputs for batch run 45074268881.

Run it from anywhere with `python3 -I`. The inputs are the artifacts and job logs of
the run, downloaded to one directory outside the repository (see INPUT_FILES for the
file names that directory must hold, and the mutation summary ZIP `c5-summary.zip`).
PyYAML must be importable.

  make_results.py --inputs DIR MUTATION_ARGS               check everything, then write
                                                            results/C10/2026-11-22.json
                                                            and .md and README.md line 39,
                                                            and print the row
  make_results.py --inputs DIR MUTATION_ARGS --check       re-render and require byte
                                                            equality with the committed files
  make_results.py --inputs DIR MUTATION_ARGS --self-test   run the real inputs, then each
                                                            single-change case; each must
                                                            fail at its named check

MUTATION_ARGS are --mutation-run, --mutation-artifact-id, --mutation-zip-sha256 and
--mutation-summary-sha256: the run and artifact of the passing verifier-c5-mutation.yml
run that meets docs/agents/factory.md "Evidence pack", and the SHA-256 of its ZIP and of
its summary.json member.

The checks run in the order G1, G2, G3, G4, G5, G6, G10, then rendering, G8, G9, and G7
only with --check. Nothing is written unless every check before G7 passes. A failure
prints `FAIL G<n>: <reason>` and exits 1. The generator reads no network and holds no
secret. Whether the mutation run's commit is on main and includes the run head is checked
outside the generator (plan acceptance step 6).
"""

import argparse
import copy
import hashlib
import io
import json
import pathlib
import re
import sys
import zipfile

import yaml

ROOT = pathlib.Path(__file__).resolve().parents[4]
REPO = "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet"
RUN_ID_NUM = 45074268881
RUN_URL = f"{REPO}/actions/runs/{RUN_ID_NUM}"
RUN_ID = "c10-45074268881-1"
RUN_HEAD = "f23fa035405cb13699235b03e98f058b3cd6cf25"
BASE = "3e7163dc641e724437025d917032fd40419e6c4d"
DATE = "2026-11-22"
JSON_PATH = "results/C10/2026-11-22.json"
MD_PATH = "results/C10/2026-11-22.md"
README_LINE = 39
ARTIFACT_EXPIRY = "2027-02-20T21:49:49Z"

EXPECTED_PATH = "scenarios/C10/expected.yaml"
JUDGED_SHA = "21097f98e69c3a8e2f77247c0f5ccd2d0b6bd5ef8b638e8866f2f838f364f34c"
SCENARIO_PATH = "scenarios/C10/scenario.yaml"
SCENARIO_SHA = "d72b4ab55fb1c05b7ef3312ced62e2ae95a65903a8c9266bda785fcf5647d290"
CONFIGS = ("C0", "C1")
REPEATS = [1, 2, 3, 4, 5]
KILL_COUNT = 110
H2_KEY = "H2_auto_store_can_pass_an_unprocessed_message"
H1_KEY = "H1_store_after_process_loses_nothing"

# Input files: key -> (file name in the inputs directory, SHA-256). The mutation ZIP is
# pinned by an argument, not here.
INPUT_FILES = {
    "c0cand": ("c10-C0-candidate.zip", "14f18dd838c796e5fddb8b96bf15dfaacfb4fb51a893c8e0cec01176e5a80953"),
    "c1cand": ("c10-C1-candidate.zip", "483c030bcbcc856d1725913a894c834b552217574c787f48564c23a88096beee"),
    "c0arch": ("c10-C0-archive.zip", "e0c7d4f3a730c78890d7c05457dae561f18ebf455ade07e0fe1fed8abef4cddb"),
    "c1arch": ("c10-C1-archive.zip", "36962b576f1ab0f3007e24c54785354fa3c0748e2c16f35c9d3c6229a1706b8d"),
    "c0pods": ("c10-C0-pods.zip", "9de6a66e8972befb2f5669316a7658f1f94a04d323bf984e56849584a81bbe9e"),
    "c1pods": ("c10-C1-pods.zip", "d22a170953de53161cfc639aa96b08ec6e29541dd804e8d65e69ad9f8750a33d"),
    "c0log": ("probe.log", "c5708404195a7f9b7e07f7fbb555865238b10e486db38dce2ddd0e73bd307069"),
    "c1log": ("full-C3.log", "3985285031dcb90ac3bea90fb5e6ed0e973f09d1830c1b9d94ad74d5b723ee82"),
}
MUTATION_ZIP_NAME = "c5-summary.zip"
ZIP_KEYS = ("c0cand", "c1cand", "c0arch", "c1arch", "c0pods", "c1pods", "c5")
MEMBER_PINS = {
    ("c0cand", "candidate.json"): "7aef903eb4f519d17d991c28085b9e2efef7203e4e4f53a8a8bb579bd15179a7",
    ("c1cand", "candidate.json"): "bdd7ab2c89f628a494c7dbf1e30363f6f3e0f106ee93303a5c66c9b356114827",
    ("c0arch", "archive-receipt.json"): "f8217c10ee6eadd1eab7a3b78ec9f152de7ac497cf7e2d0578686f6761d1edad",
    ("c0arch", "archive-manifest.json"): "c474ac169f0ea2ed66600719fa041f51bd19160f1420a4f1ffe6f4727624f960",
    ("c1arch", "archive-receipt.json"): "e40fb4b8e5afe967d825d84096d869dba51df24ce23886e09ef9c0eff2fff3a1",
    ("c1arch", "archive-manifest.json"): "2ee1d7fad27daeec0302e6a2084d746c57e4586a3e53bacc9a1a40dbf0252ec2",
    ("c0pods", "phase-status.json"): "98604aa1258ad08e07fc01ee4ed8bcec4bb5defabf9c7d6d965f18fa6d67ffc7",
    ("c1pods", "phase-status.json"): "ab8a929dc4b00b88096de2b110c396df610c24aa64ba3a45bc967999ee1c6a6b",
}
ARTIFACTS = {
    "C0": [("candidate", 14776044642, "c10-C0-candidate-45074268881-1", "c0cand"),
           ("archive", 14776529837, "c10-C0-archive-45074268881-1", "c0arch"),
           ("pods", 14775989790, "c10-C0-pods-45074268881-1", "c0pods")],
    "C1": [("candidate", 14793443758, "c10-C1-candidate-45074268881-1", "c1cand"),
           ("archive", 14794880764, "c10-C1-archive-45074268881-1", "c1arch"),
           ("pods", 14793209973, "c10-C1-pods-45074268881-1", "c1pods")],
}
LOGS = {"C0": (137367883128, "probe", "c0log"), "C1": (137388417872, "full-C3", "c1log")}
# Log line numbers of the Terraform plan lines, in the order sku, capacity, location.
LOG_LINES = {"C0": (1582, 1567, 1576), "C1": (1409, 1394, 1403)}
JOBS = ("consumer-subject", "consumer-witness", "start-line", "loadgen", "verify")

TITLE = ("# C10: a consumer killed while holding an unprocessed message left no message marked done that nobody "
         "processed; some messages were processed twice")

SUMMARY_TEMPLATE = (
    "In run 45074268881 on Event Hubs Premium, a consumer in a two-member group was killed while it held a message "
    "it had not processed, and the other member took over its partitions; in all five repeats that stored each "
    "offset only after processing the message, and in all five that used librdkafka's default of storing it when "
    "the message is handed to the application, no message was marked done without being processed. In every "
    "repeat some messages were processed twice ({c0} per repeat when storing after processing, {c1} with the "
    "default), which the librdkafka sources explain: after the crash the surviving member redoes the work done "
    "since the last periodic commit. Storing after processing lost nothing, so the proposed verdict is "
    "`confirmed`; the zero under the default store does not show that the default is safe, and the workload is "
    "synthetic."
)
VERDICT_LINE = "Proposed verdict: `confirmed`."
PLAIN_HEADING = "## In plain words"
PLAIN_WORDS = [
    "**The production event.** A consumer has been running in a group and committing its progress. It is killed "
    "while it holds a message it has not processed yet, and another member of the group takes over its "
    "partitions. The question is whether that crash leaves any message marked done that nobody processed. "
    "\"Marked done\" means the group's committed offset for the partition has moved past the message, so no "
    "member will read it again.",
    "**The two settings.** With the first setting (C0, the baseline), the application stores a message's offset "
    "only after it has processed the message. With the second (C1), librdkafka's default "
    "`enable.auto.offset.store` stores the offset as soon as the message is handed to the application, before "
    "the application has processed it. Only the killed consumer changes setting; the member that takes over "
    "runs the baseline in both. In both, librdkafka commits the stored offsets in the background every 5000 ms.",
    "**What was measured.** In every repeat of both settings, zero messages were marked done without being "
    "processed. Some messages were processed twice: {c0} per repeat when storing after processing, and {c1} per "
    "repeat with the default.",
    "**What the verdict means.** Storing after processing lost nothing, so the prediction is confirmed. The zero "
    "under the default store does not show that the default is safe. At this kill point a message can be marked "
    "done without being processed only if librdkafka commits in the short time between handing the message over "
    "and the kill, which is unlikely.",
    "**Why some messages were processed twice.** Both settings showed it. It is the cost of committing "
    "periodically, called at-least-once delivery: after a crash, the surviving member starts from the last "
    "committed offset and redoes the work done since that commit. The run counted these messages; that they come "
    "from this cause is an inference from the librdkafka sources listed under Sources, not a measurement.",
    "**What this result does not cover.** A consumer that dies before its first commit is a different failure "
    "with a different cause. It is tested under C01 (#513), not here.",
]
WHAT_TESTED = (
    "Each repeat ran two consumers in one group: a subject that carried the fault and a witness that ran the "
    "baseline consumer and received the subject's partitions. The verifier counted 240 delivered synthetic events "
    "per repeat, in five repeats per configuration. C0 runs the baseline, which stores an offset after "
    "processing. C1 sets `enable.auto.offset.store` to `true` on the subject and changes nothing else; the "
    "witness runs the baseline in both configurations."
)
COMMIT_PRECONDITION_SENTENCE = (
    "No repeat failed the commit precondition: in all ten, every qualifying partition had a successful subject "
    "commit before the kill."
)
RESULTS_SENTENCE = (
    "\"Committed, never processed\" is the verifier's name for a message marked done that nobody processed. "
    "\"Processed again\" counts messages with more than one processing row; \"Extra processing rows\" counts the "
    "rows beyond the first, and the two are equal in every repeat, so each such message was processed exactly "
    "twice."
)
H1_LINE = ("- H1, storing after processing loses nothing (C0): holds. All five repeats counted, and in each no "
           "message was committed without being processed.")
H2_LINE = ("- H2, the default automatic store can pass an unprocessed message (C1), published on its own line and "
           "not part of the verdict: inconclusive. All five repeats counted, and none had a message committed "
           "without being processed; a zero count does not contradict a possibility.")
PROCESSED_AGAIN_LINE = ("- Processed again, recorded only and published on its own line: {c0} messages per repeat in "
                        "C0 and {c1} in C1.")
COMMIT_EVIDENCE = (
    "After each repeat the verifier read the group's committed offsets from Event Hubs without joining the group "
    "(`offset_read`, outcome `success` in all ten repeats). A repeat is complete and valid only when every committed offset it "
    "read matches an offset the members' own commit records report, and only when, on every partition that "
    "received messages during the run, the committed offset reached the run's end line. So \"committed\" here is "
    "observed from the broker, not only reported by the consumers."
)
BROKER_SENTENCE = ("Broker connection counts are the load generator producer's only, and are lower bounds, as each "
                   "repeat's `broker_count.lower_bound` records.")
SELF_KILL_SENTENCE = (
    "The subject has no native-monitoring evidence; the archive records its missing index as "
    "`configured_self_kill` in each of the ten repeats."
)
NODE_SENTENCE = ("Both consumers of every repeat ran on the same `workload` node; every subject exited with code "
                 "137 (SIGKILL) and every witness exited 0.")
NOT_TESTED = [
    "- A consumer that dies before its first commit (K-57): tested under C01, not C10.",
    "- Loss under the default automatic store at a kill point other than the 110th message, or when a periodic "
    "commit lands between hand-over and the kill: not observed (H2).",
]
README_FRAGMENT = "The zero under the default does not show"
README_ROW_TEMPLATE = (
    "| C10 | Offset storage and crash outcomes | A consumer killed while holding a message it had not processed, "
    "with another member taking over its partitions, left no message marked done that nobody processed: zero in "
    "all five repeats that stored each offset only after processing, and zero in all five that used librdkafka's "
    "default of storing it when the message is handed over. Some messages were processed twice in every repeat "
    "({c0} per repeat after processing, {c1} with the default); inferred from the librdkafka sources, after a "
    "crash the surviving member redoes the work done since the last periodic commit. The zero under the default "
    "does not show that the default is safe: loss there needs a commit to land between hand-over and the kill, "
    "which is unlikely at this kill point. A consumer that dies before its first commit is tested under C01. "
    "Synthetic workload. | confirmed | [Report](results/C10/2026-11-22.md) |"
)

BLOB = f"{REPO}/blob/{BASE}/"
BLOB_HEAD = f"{REPO}/blob/{RUN_HEAD}/"
VERIFIER_TERMS = ("committed_never_processed", "processed_again", "subject_commit_coverage", "offset_read",
                  "complete_valid")


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


def read_inputs(directory):
    raw = {}
    names = {key: name for key, (name, _pin) in INPUT_FILES.items()}
    names["c5"] = MUTATION_ZIP_NAME
    for key, name in names.items():
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


def load_yaml(data, label):
    try:
        return yaml.safe_load(data.decode("utf-8"))
    except (yaml.YAMLError, UnicodeDecodeError) as error:
        fail("G1", f"{label} is not valid YAML: {error}")


def new_context(raw, args):
    ctx = Ctx()
    ctx.raw = raw
    ctx.args = dict(args)
    ctx.members = {key: open_zip(raw[key], key) for key in ZIP_KEYS}
    ctx.working_expected = (ROOT / EXPECTED_PATH).read_bytes()
    ctx.working_scenario = (ROOT / SCENARIO_PATH).read_bytes()
    return ctx


# ----------------------------------------------------------------------------------------------
# Checks


def check_g1(ctx):
    for key, (name, pin) in INPUT_FILES.items():
        expect(sha256(ctx.raw[key]) == pin, "G1", f"{name} differs from its pinned SHA-256")
    for (key, member), pin in MEMBER_PINS.items():
        expect(member in ctx.members[key], "G1", f"{key} has no member {member}")
        expect(sha256(ctx.members[key][member]) == pin, "G1", f"member {member} of {key} differs from its pin")
    expect(sha256(ctx.raw["c5"]) == ctx.args["zip_sha256"], "G1", "the mutation ZIP differs from --mutation-zip-sha256")
    expect("summary.json" in ctx.members["c5"], "G1", "the mutation ZIP has no member summary.json")
    expect(sha256(ctx.members["c5"]["summary.json"]) == ctx.args["summary_sha256"], "G1",
           "the mutation summary.json differs from --mutation-summary-sha256")
    expect(sha256(ctx.working_expected) == JUDGED_SHA, "G1", "expected.yaml in the working tree differs from its pin")
    expect(sha256(ctx.working_scenario) == SCENARIO_SHA, "G1", "scenario.yaml in the working tree differs from its pin")


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


def parse_yaml(ctx):
    ctx.judged = load_yaml(ctx.working_expected, "expected.yaml")


def parse(ctx):
    ctx.cand = {
        "C0": strict_json(ctx.members["c0cand"]["candidate.json"], "C0 candidate"),
        "C1": strict_json(ctx.members["c1cand"]["candidate.json"], "C1 candidate"),
    }
    ctx.receipt = {
        "C0": strict_json(ctx.members["c0arch"]["archive-receipt.json"], "C0 archive receipt"),
        "C1": strict_json(ctx.members["c1arch"]["archive-receipt.json"], "C1 archive receipt"),
    }
    ctx.manifest = {
        "C0": strict_json(ctx.members["c0arch"]["archive-manifest.json"], "C0 archive manifest"),
        "C1": strict_json(ctx.members["c1arch"]["archive-manifest.json"], "C1 archive manifest"),
    }
    ctx.phase = {
        "C0": strict_json(ctx.members["c0pods"]["phase-status.json"], "C0 phase status"),
        "C1": strict_json(ctx.members["c1pods"]["phase-status.json"], "C1 phase status"),
    }
    ctx.summary = strict_json(ctx.members["c5"]["summary.json"], "mutation summary")
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
        expect(c.get("scenario_id") == "C10" and c.get("configuration_id") == phase and c.get("phase") == phase
               and c.get("run_id") == RUN_ID, "G2", f"{phase} candidate identity")
        expect(c.get("source_commit") == RUN_HEAD and c.get("synthetic") is True, "G2",
               f"{phase} candidate source commit or synthetic flag")
        expect(c.get("status") == "complete_valid" and c.get("reasons") == [], "G2", f"{phase} candidate status or reasons")
        consumer = c.get("consumer", {})
        expect(consumer.get("status") == "complete_valid" and consumer.get("reasons") == [], "G2",
               f"{phase} root consumer status or reasons")
        repeats = c.get("repeats")
        expect(isinstance(repeats, list) and [r.get("repeat") for r in repeats] == REPEATS, "G2",
               f"{phase} repeats are not 1 to 5")
        for r in repeats:
            expect(r.get("status") == "complete_valid" and r.get("reasons") == [], "G2",
                   f"{phase} repeat {r.get('repeat')} status or reasons")
            expect(r["consumer"].get("status") == "complete_valid" and r["consumer"].get("reasons") == [], "G2",
                   f"{phase} repeat {r['repeat']} consumer status or reasons")
    expect(ctx.cand["C0"]["versions"] == ctx.cand["C1"]["versions"], "G2", "the two candidates' versions differ")


def counted(r):
    return (r["status"] == "complete_valid" and r["consumer"]["status"] == "complete_valid"
            and r["consumer"]["subject_commit_coverage"]["uncovered_partitions"] == [])


def evaluate(reps):
    """H1 and H2 from the repeats of one configuration, as scenarios/C10/expected.yaml writes them."""
    all_counted = [r["repeat"] for r in reps] == REPEATS and all(counted(r) for r in reps)
    losses = [r["consumer"]["counts"]["committed_never_processed"] for r in reps]
    h1 = "holds" if all_counted and all(n == 0 for n in losses) else (
        "contradicted" if all_counted and any(n >= 1 for n in losses) else "inconclusive")
    h2 = "holds" if all_counted and any(n >= 1 for n in losses) else "inconclusive"
    return h1, h2


def recompute(ctx):
    c0, c1 = ctx.cand["C0"]["repeats"], ctx.cand["C1"]["repeats"]
    h1, _ = evaluate(c0)
    _, h2 = evaluate(c1)
    verdict = "confirmed" if h1 == "holds" else "contradicted" if h1 == "contradicted" else "no_verdict"
    return {"H1": h1, "H2": h2, "verdict": verdict}


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
    expect(got["H2"] == "inconclusive", "G3", f"H2 recomputes to {got['H2']}, not inconclusive")
    expect(got["verdict"] == "confirmed", "G3", f"verdict recomputes to {got['verdict']}, not confirmed")
    for phase in CONFIGS:
        expect(all(counted(r) for r in ctx.cand[phase]["repeats"]), "G3", f"{phase} has a repeat that failed the commit precondition")
    expect([r["consumer"]["counts"]["processed_again"] for r in ctx.cand["C0"]["repeats"]] == [2, 4, 2, 3, 4], "G3",
           "C0 processed_again is not [2, 4, 2, 3, 4]")
    expect([r["consumer"]["counts"]["processed_again"] for r in ctx.cand["C1"]["repeats"]] == [4, 1, 2, 2, 5], "G3",
           "C1 processed_again is not [4, 1, 2, 2, 5]")
    for phase in CONFIGS:
        for r in ctx.cand[phase]["repeats"]:
            c = r["consumer"]
            where = f"{phase} repeat {r['repeat']}"
            expect(c["kill_count"] == KILL_COUNT, "G3", f"{where}: kill_count is not {KILL_COUNT}")
            expect(c["counts"]["processed_again_extra_rows"] == c["counts"]["processed_again"], "G3",
                   f"{where}: processed_again_extra_rows differs from processed_again")
            expect(c["counts"]["processed_again"] >= 1, "G3", f"{where}: no message was processed again")
            expect(c["offset_read"]["outcome"] == "success", "G3", f"{where}: offset_read outcome is not success")
            nodes = []
            for role, want in (("consumer-subject", 137), ("consumer-witness", 0)):
                items, node, codes = pod_facts(ctx.pods[(phase, r["repeat"], role)])
                expect(len(items) == 1 and len(node) == 1 and None not in node and codes and all(x == want for x in codes),
                       "G3", f"{where} {role}: not one pod on one node with every container exited {want}")
                nodes.append(next(iter(node)))
            expect(nodes[0] == nodes[1], "G3", f"{where}: subject and witness ran on different nodes")
        kills = [o for o in ctx.manifest[phase]["objects"] if o.get("gap_code") == "configured_self_kill"]
        expect(sorted(o.get("repeat") for o in kills) == REPEATS, "G3", f"{phase} manifest: configured_self_kill entries are not repeats 1 to 5")
        expect(all(o.get("kind") == "native_monitoring_index" and "-consumer-subject/" in o.get("source", {}).get("name", "")
                   for o in kills), "G3", f"{phase} manifest: a configured_self_kill entry is not a subject native_monitoring_index")


def check_g4(ctx):
    for phase in CONFIGS:
        c = ctx.cand[phase]
        for key in c["counts"]:
            expect(c["counts"][key] == sum(r["counts"][key] for r in c["repeats"]), "G4",
                   f"{phase} root counts.{key} is not the sum over repeats")
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
        expect(ident.get("github_run_id") == str(RUN_ID_NUM) and ident.get("github_run_attempt") == 1
               and ident.get("phase") == phase and ident.get("run_id") == RUN_ID and ident.get("source_commit") == RUN_HEAD,
               "G5", f"{phase} archive receipt identity")
        expect(rc.get("manifest", {}).get("sha256") == manifest_pin, "G5", f"{phase} receipt manifest hash differs from the manifest member")


def check_g6(ctx):
    for phase in CONFIGS:
        ps = ctx.phase[phase]
        expect(ps.get("phase") == phase and ps.get("run_id") == RUN_ID and ps.get("safety_stop") is False, "G6",
               f"{phase} phase status phase, run_id or safety_stop")
        cand = ps.get("candidate", {})
        expect(cand.get("status") == "complete_valid" and cand.get("repeats") == [[n, "complete_valid"] for n in REPEATS], "G6",
               f"{phase} phase status candidate")
        modes = ps.get("modes", [])
        expect(len(modes) == 31, "G6", f"{phase} phase status has {len(modes)} modes, not 31")
        for mode in modes:
            name = mode.get("mode")
            allowed = "failed" if name == "consumer-subject" else "ready" if name == "consumer-readiness" else "complete"
            expect(mode.get("outcome") == allowed, "G6", f"{phase} phase status Job {mode.get('job')} outcome {mode.get('outcome')}")


def check_g10(ctx):
    s = ctx.summary
    expect(s.get("measurement_complete") is True and s.get("break_passed") is True, "G10",
           "mutation summary measurement_complete or break_passed is not true")
    expect(s.get("run_id") == str(ctx.args["run"]), "G10", "mutation summary run_id differs from --mutation-run")
    expect(isinstance(s.get("commit"), str) and re.fullmatch(r"[0-9a-f]{40}", s["commit"]) is not None, "G10",
           "mutation summary commit is not a 40-character lowercase hexadecimal SHA")


def check_g8(md, row):
    expect(md.isascii() and row.isascii(), "G8", "the rendered report or README row has a byte outside ASCII")


def check_g9(ctx, md, row):
    expect(md.startswith(TITLE + "\n\n"), "G9", "the report does not start with the pinned title")
    expect(SUMMARY_TEMPLATE.format(**ranges(ctx)) in md, "G9", "the summary paragraph differs from the pinned text")
    expect(VERDICT_LINE in md, "G9", "the verdict line is missing")
    expect(PLAIN_HEADING in md, "G9", "the plain-words heading is missing")
    expect("## What was tested" in md and md.index(PLAIN_HEADING) < md.index("## What was tested"), "G9",
           "the plain-words section does not come before What was tested")
    for paragraph in PLAIN_WORDS:
        expect(paragraph.format(**ranges(ctx)) in md, "G9", "a plain-words paragraph is missing or changed")
    before = md[len(TITLE):md.index(PLAIN_HEADING)]
    for term in VERIFIER_TERMS:
        expect(term not in before, "G9", f"the verifier term {term} comes before the plain-words section")
    expect(ctx.judged["assessment_rules"]["kill_point"] in md, "G9", "the kill point quotation is missing")
    expect(ctx.judged["assessment_rules"]["commit_precondition"] in md, "G9", "the commit precondition quotation is missing")
    h2_text = ctx.judged["configurations"]["C1"][H2_KEY]["published_text_must_state"]
    expect("\n  " + h2_text + "\n" in md, "G9", "the H2 indented line is missing")
    expect(SELF_KILL_SENTENCE in md, "G9", "the configured_self_kill sentence is missing")
    for line in NOT_TESTED:
        expect(line in md, "G9", "a Not tested line is missing")
    expect(README_FRAGMENT in row, "G9", "the README row lacks the sentence about the default store")


# ----------------------------------------------------------------------------------------------
# Publication


def rng(ctx, phase):
    values = [r["consumer"]["counts"]["processed_again"] for r in ctx.cand[phase]["repeats"]]
    return f"{min(values)} to {max(values)}"


def ranges(ctx):
    return {"c0": rng(ctx, "C0"), "c1": rng(ctx, "C1")}


def build_publication(ctx):
    cands = ctx.cand
    ar = ctx.judged["assessment_rules"]
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
    summary_name = f"c5-summary-{ctx.summary['run_id']}-{ctx.summary['run_attempt']}"
    inputs.append({"name": "mutation summary artifact ZIP",
                   "source": f"artifact {ctx.args['artifact_id']}, {summary_name}", "sha256": ctx.args["zip_sha256"]})
    inputs.append({"name": "mutation summary.json", "source": f"member summary.json of artifact {ctx.args['artifact_id']}",
                   "sha256": ctx.args["summary_sha256"]})
    inputs.append({"name": EXPECTED_PATH, "source": "working tree, equal to the base commit and the run head: the expected outcome the run was judged against",
                   "sha256": JUDGED_SHA})
    inputs.append({"name": SCENARIO_PATH, "source": "working tree, equal to the base commit and the run head", "sha256": SCENARIO_SHA})

    def per(phase, role):
        return [pod_facts(ctx.pods[(phase, repeat, role)]) for repeat in REPEATS]

    nodes = {phase: [next(iter(f[1])) for f in per(phase, "consumer-subject")] for phase in CONFIGS}
    exit_codes = {phase: {"subject": [f[2][0] for f in per(phase, "consumer-subject")],
                          "witness": [f[2][0] for f in per(phase, "consumer-witness")]} for phase in CONFIGS}
    again = {phase: [r["consumer"]["counts"]["processed_again"] for r in cands[phase]["repeats"]] for phase in CONFIGS}
    extra = {phase: [r["consumer"]["counts"]["processed_again_extra_rows"] for r in cands[phase]["repeats"]] for phase in CONFIGS}
    loss = {phase: [r["consumer"]["counts"]["committed_never_processed"] for r in cands[phase]["repeats"]] for phase in CONFIGS}
    publication = {
        "item": "C10",
        "workload": "synthetic",
        "run": {
            "workflow_run_id": RUN_ID_NUM, "attempt": 1, "event": "workflow_dispatch", "ref": "main", "head_sha": RUN_HEAD,
            "created_at": "2026-11-22T21:49:48Z", "conclusion": "success", "run_id": RUN_ID,
        },
        "expected_outcome": {"path": EXPECTED_PATH, "sha256": JUDGED_SHA},
        "scenario_definition": {"path": SCENARIO_PATH, "sha256": SCENARIO_SHA},
        "publication_decision": {
            "comment_id": 7126256621, "url": f"{REPO}/issues/512#issuecomment-7126256621",
        },
        "proposed_verdict": "confirmed",
        "rule": "scenarios/C10/expected.yaml verdict.confirmed_when: H1 holds.",
        "kill_point": ar["kill_point"],
        "commit_precondition": {phase: {"met": [r["repeat"] for r in cands[phase]["repeats"] if counted(r)],
                                        "failed": [r["repeat"] for r in cands[phase]["repeats"] if not counted(r)]}
                                for phase in CONFIGS},
        "hypotheses": {
            H1_KEY: {"configuration": "C0", "result": "holds", "committed_never_processed": loss["C0"]},
            H2_KEY: {"configuration": "C1", "result": "inconclusive", "committed_never_processed": loss["C1"],
                     "separate_line": True},
        },
        "processed_again": {
            "mode": "recorded_only", "separate_line": True,
            "C0": {"processed_again": again["C0"], "processed_again_extra_rows": extra["C0"]},
            "C1": {"processed_again": again["C1"], "processed_again_extra_rows": extra["C1"]},
        },
        "archives": {
            phase: {"archive_complete": ctx.receipt[phase]["archive_complete"], "gaps": ctx.receipt[phase]["gaps"],
                    "manifest_sha256": ctx.receipt[phase]["manifest"]["sha256"],
                    "configured_self_kill_repeats": sorted(
                        o["repeat"] for o in ctx.manifest[phase]["objects"] if o.get("gap_code") == "configured_self_kill")}
            for phase in CONFIGS
        },
        "consumer_nodes": nodes,
        "consumer_exit_codes": exit_codes,
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
            "workflow_run_id": int(ctx.args["run"]), "commit": ctx.summary["commit"],
            "measurement_complete": ctx.summary["measurement_complete"], "break_passed": ctx.summary["break_passed"],
            "summary_sha256": ctx.args["summary_sha256"], "includes_run_head": RUN_HEAD,
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


def render_row(ctx):
    return README_ROW_TEMPLATE.format(**ranges(ctx))


def repeat_rows(ctx, phase):
    rows = []
    for r in ctx.cand[phase]["repeats"]:
        c = r["consumer"]
        cov = c["subject_commit_coverage"]["uncovered_partitions"]
        rows.append([r["repeat"], r["status"], c["status"], "met" if not cov else f"failed: {cov}",
                     c["counts"]["processed_once"], c["counts"]["processed_again"],
                     c["counts"]["processed_again_extra_rows"], c["counts"]["committed_never_processed"]])
    return rows


def render_md(ctx, document):
    publication = document["publication"]
    cands = ctx.cand
    rg = ranges(ctx)
    ar = ctx.judged["assessment_rules"]
    out = [TITLE, "", SUMMARY_TEMPLATE.format(**rg), "", VERDICT_LINE, "", PLAIN_HEADING, ""]
    for paragraph in PLAIN_WORDS:
        out += [paragraph.format(**rg), ""]

    out += ["## What was tested", "", WHAT_TESTED, "", "Kill point (from the expected outcome):", "", "> " + ar["kill_point"], "",
            "Commit precondition (from the expected outcome):", "", "> " + ar["commit_precondition"], "",
            COMMIT_PRECONDITION_SENTENCE, ""]

    headers = ["Repeat", "Status", "Consumer status", "Commit precondition", "Processed once", "Processed again",
               "Extra processing rows", "Committed, never processed"]
    out += ["## Results by repeat", "", "**C0, store after processing**", "", table(headers, repeat_rows(ctx, "C0")), "",
            "**C1, default automatic store**", "", table(headers, repeat_rows(ctx, "C1")), "", RESULTS_SENTENCE, ""]

    h2_text = ctx.judged["configurations"]["C1"][H2_KEY]["published_text_must_state"]
    out += ["## Hypotheses", "", H1_LINE, H2_LINE, "  " + h2_text, PROCESSED_AGAIN_LINE.format(**rg), ""]

    out += ["## Commit evidence", "", COMMIT_EVIDENCE, ""]

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
        f"- Run [{RUN_ID_NUM}]({RUN_URL}), attempt 1, event `workflow_dispatch`, ref `main`, head `{RUN_HEAD}`, created "
        "2026-11-22T21:49:48Z, conclusion `success`. Jobs `probe` (C0) and `full-C3` (C1) succeeded; `batch`, `batch-stop` "
        "and `full-C0` were skipped.",
        "- Both candidates are `complete_valid` with no reasons.",
        "- Both phase-status files report `safety_stop` false, every `consumer-subject` Job `failed` (the subject ends "
        "itself with SIGKILL, as the kill point requires), every `consumer-readiness` Job `ready` and every other Job `complete`.",
    ]
    for phase in CONFIGS:
        rc = ctx.receipt[phase]
        out += [f"- {phase} archive receipt: `archive_complete` true, no gaps, manifest SHA-256 `{rc['manifest']['sha256']}`."]
    out += ["", SELF_KILL_SENTENCE, "", "Artifacts of the run (each ZIP's SHA-256 is in the JSON `inputs` list):", ""]
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
        f"`{LOGS['C0'][1]}` log (C0) lines {LOG_LINES['C0'][0]}, {LOG_LINES['C0'][1]} and {LOG_LINES['C0'][2]}; "
        f"`{LOGS['C1'][1]}` log (C1) lines {LOG_LINES['C1'][0]}, {LOG_LINES['C1'][1]} and {LOG_LINES['C1'][2]} "
        "(`sku`, `capacity`, `location`; the lines carry colour escapes and are cited by number, not quoted).",
        "- AKS node size Standard_D4ds_v5 is inferred from `infra/cluster/aks.tf` lines 35, 44 and 51 at the base commit, not captured from the run.",
        f"- Versions: Confluent.Kafka `{v['confluent_kafka']}`, librdkafka `{v['librdkafka']}`, .NET `{v['dotnet']}`, "
        f"verifier `{v['verifier']}`, workload generator `{v['workload_generator']}`.",
        f"- Source commit: `{RUN_HEAD}`.",
    ]
    for phase in CONFIGS:
        t = cands[phase]["timings"]
        out += [f"- {phase}: worker started {t['worker_started_utc']}, judge completed {t['judge_completed_utc']}."]
    out += [""]

    m = publication["mutation_acceptance"]
    out += [
        "## Mutation acceptance", "",
        f"C5 mutation run [{m['workflow_run_id']}]({REPO}/actions/runs/{m['workflow_run_id']}) on `{m['commit']}` "
        f"reports `measurement_complete` {str(m['measurement_complete']).lower()} and `break_passed` {str(m['break_passed']).lower()} "
        f"(summary SHA-256 `{m['summary_sha256']}`). That commit is on `main` and includes `{RUN_HEAD}`, the run's head "
        "(docs/agents/factory.md \"Evidence pack\").", "",
    ]

    out += ["## Not tested", ""] + NOT_TESTED + [""]

    lib = "https://github.com/confluentinc/librdkafka/blob/v2.16.0/"
    conf = "https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/"
    out += ["## Sources", "",
            f"- Run [{RUN_ID_NUM}]({RUN_URL}), its artifacts and job logs, as listed above and in the JSON `inputs`.",
            f"- Scenario definition and expected outcome as approved: [scenarios/C10/scenario.yaml]({BLOB_HEAD}{SCENARIO_PATH}) "
            f"(SHA-256 `{SCENARIO_SHA}`) and [scenarios/C10/expected.yaml]({BLOB_HEAD}{EXPECTED_PATH}) (SHA-256 `{JUDGED_SHA}`), "
            f"both at `{RUN_HEAD}`, as the run was judged.",
            f"- Publication decision: [7126256621]({REPO}/issues/512#issuecomment-7126256621) (the plain-words points).",
            f"- librdkafka v2.16.0 [`CONFIGURATION.md`]({lib}CONFIGURATION.md) lines 138, 139, 140 and 206; "
            f"[`src/rdkafka.c`]({lib}src/rdkafka.c) lines 5126 to 5128 and 5183 to 5193; "
            f"[`src/rdkafka_op.c`]({lib}src/rdkafka_op.c) lines 1130 to 1156; "
            f"[`src/rdkafka_offset.c`]({lib}src/rdkafka_offset.c) lines 674 and 1558; "
            f"[`src/rdkafka_cgrp.c`]({lib}src/rdkafka_cgrp.c) lines 552 to 557; "
            f"[`src/rdkafka_assignment.c`]({lib}src/rdkafka_assignment.c) lines 214 to 220, 503 to 512 and 631 to 650.",
            f"- Confluent.Kafka v2.16.0 [`Consumer.cs`]({conf}src/Confluent.Kafka/Consumer.cs) lines 470 to 486 and 909 to 911, "
            f"and [`SafeKafkaHandle.cs`]({conf}src/Confluent.Kafka/Impl/SafeKafkaHandle.cs) line 764.",
            f"- This repository at `{BASE}`: "
            f"[`BaselineSettingsCatalog.cs`]({BLOB}src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs) lines 179, 181 and 183; "
            f"[`ScenarioConsumerProcessingPlanSource.cs`]({BLOB}src/Parchwell.Enrichment/ConsumerProcessing/ScenarioConsumerProcessingPlanSource.cs) line 11; "
            f"[`ConsumerJudgement.cs`]({BLOB}src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs) lines 63 to 74; "
            f"[`ConsumerCommittedOffsetReader.cs`]({BLOB}src/Parchwell.Verifier/VerifyRun/Consumer/Offsets/ConsumerCommittedOffsetReader.cs) line 11; "
            f"[`aks.tf`]({BLOB}infra/cluster/aks.tf) lines 35, 44 and 51; "
            f"[docs/blueprint.md]({BLOB}docs/blueprint.md) lines 756 and 792 (ADR-022) and line 1276 (K-57 under C01).",
            f"- C5 mutation run [{m['workflow_run_id']}]({REPO}/actions/runs/{m['workflow_run_id']}) and its `summary.json`, as in Mutation acceptance.",
            "- Generator and receipt: [make_results.py](../../docs/operations/evidence/issue-512-c10-verdict/make_results.py) "
            "and [receipt.md](../../docs/operations/evidence/issue-512-c10-verdict/receipt.md).",
            ""]
    return "\n".join(out).rstrip("\n") + "\n"


# ----------------------------------------------------------------------------------------------
# Pipeline


def pipeline(raw, args, mode="render", hooks=None, committed=None):
    hooks = hooks or {}

    def hook(stage, *a):
        if stage in hooks:
            hooks[stage](*a)

    ctx = new_context(raw, args)
    hook("args", ctx)
    hook("members", ctx)
    hook("working", ctx)
    check_g1(ctx)
    parse_yaml(ctx)
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
    check_g8(md, row)
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
    expect(lines[README_LINE - 1].startswith("| C10 |"), "G1", f"README.md line {README_LINE} is not the C10 row")
    (ROOT / "results" / "C10").mkdir(parents=True, exist_ok=True)
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

    def set_(target, key, value):
        target[key] = value

    def node_split(ctx):
        ctx.pods[("C1", 2, "consumer-witness")]["items"][0]["spec"]["nodeName"] = "another-node"

    def subject_exit_zero(ctx):
        for item in ctx.pods[("C0", 5, "consumer-subject")]["items"]:
            for status in item["status"]["containerStatuses"]:
                status["state"]["terminated"]["exitCode"] = 0

    def self_kill_missing(ctx):
        for o in ctx.manifest["C1"]["objects"]:
            if o.get("gap_code") == "configured_self_kill" and o.get("repeat") == 3:
                o["gap_code"] = None
                return
        fail("SELFTEST", "no repeat 3 configured_self_kill entry in the C1 manifest")

    def subject_mode_complete(ctx):
        for mode in ctx.phase["C1"]["modes"]:
            if mode.get("repeat") == 1 and mode.get("mode") == "consumer-subject":
                mode["outcome"] = "complete"
                return
        fail("SELFTEST", "no repeat 1 consumer-subject mode in the C1 phase status")

    def md_replace(old, new):
        def apply(box):
            expect(old in box[0], "SELFTEST", f"case text not found: {old[:40]}")
            box[0] = box[0].replace(old, new, 1)

        return {"md": apply}

    def committed_edit(files):
        expect("| 1 | complete_valid |" in files["md"], "SELFTEST", "committed report has no repeat 1 row")
        files["md"] = files["md"].replace("| 1 | complete_valid |", "| 1 | complete_invalid |", 1)

    def args_hash(ctx):
        value = ctx.args["summary_sha256"]
        ctx.args["summary_sha256"] = value[:-1] + ("0" if value[-1] != "0" else "1")

    def mutation_run_id(ctx):
        ctx.summary["run_id"] = "45028998990"

    plain4 = PLAIN_WORDS[3]
    return [
        ("hash_c0_candidate", "G1", {"members": lambda ctx: ctx.members["c0cand"].__setitem__(
            "candidate.json", flip(ctx.members["c0cand"]["candidate.json"]))}),
        ("expected_yaml_edit", "G1", {"working": lambda ctx: setattr(ctx, "working_expected", flip(ctx.working_expected))}),
        ("mutation_hash", "G1", {"args": args_hash}),
        ("c1_repeat_status", "G2", parsed(lambda ctx: set_(rep(ctx, "C1", 3)["consumer"], "status", "incomplete"))),
        ("versions_differ", "G2", parsed(lambda ctx: set_(ctx.cand["C1"]["versions"], "librdkafka", "2.15.0"))),
        ("h1_loss_one", "G3", parsed(lambda ctx: set_(rep(ctx, "C0", 2)["consumer"]["counts"], "committed_never_processed", 1))),
        ("h1_precondition_failed", "G3", parsed(lambda ctx: set_(
            rep(ctx, "C0", 4)["consumer"]["subject_commit_coverage"], "uncovered_partitions", [3]))),
        ("h2_loss_one", "G3", parsed(lambda ctx: set_(rep(ctx, "C1", 5)["consumer"]["counts"], "committed_never_processed", 1))),
        ("h2_precondition_failed", "G3", parsed(lambda ctx: set_(
            rep(ctx, "C1", 1)["consumer"]["subject_commit_coverage"], "uncovered_partitions", [0]))),
        ("extra_rows_differ", "G3", parsed(lambda ctx: set_(rep(ctx, "C0", 3)["consumer"]["counts"], "processed_again_extra_rows", 3))),
        ("processed_again_zero", "G3", parsed(lambda ctx: (
            set_(rep(ctx, "C1", 2)["consumer"]["counts"], "processed_again", 0),
            set_(rep(ctx, "C1", 2)["consumer"]["counts"], "processed_again_extra_rows", 0)))),
        ("kill_count", "G3", parsed(lambda ctx: set_(rep(ctx, "C1", 4)["consumer"], "kill_count", 111))),
        ("offset_read_failed", "G3", parsed(lambda ctx: set_(rep(ctx, "C0", 1)["consumer"]["offset_read"], "outcome", "kafka_exception"))),
        ("subject_exit_zero", "G3", parsed(subject_exit_zero)),
        ("node_split", "G3", parsed(node_split)),
        ("self_kill_missing", "G3", parsed(self_kill_missing)),
        ("root_consumer_sum", "G4", parsed(lambda ctx: set_(ctx.cand["C0"]["consumer"]["counts"], "processed_again", 14))),
        ("root_producer_sum", "G4", parsed(lambda ctx: set_(ctx.cand["C1"]["counts"], "ok", 1199))),
        ("archive_gap", "G5", parsed(lambda ctx: set_(ctx.receipt["C0"], "gaps", ["x"]))),
        ("archive_phase", "G5", parsed(lambda ctx: set_(ctx.receipt["C1"]["identity"], "phase", "C0"))),
        ("phase_safety_stop", "G6", parsed(lambda ctx: set_(ctx.phase["C0"], "safety_stop", True))),
        ("subject_mode_complete", "G6", parsed(subject_mode_complete)),
        ("mutation_incomplete", "G10", parsed(lambda ctx: set_(ctx.summary, "measurement_complete", False))),
        ("mutation_run_id", "G10", parsed(mutation_run_id)),
        ("non_ascii", "G8", md_replace("## Sources", "## Sources " + chr(0x2013))),
        ("missing_plain_words", "G9", md_replace(plain4 + "\n\n", "")),
        ("verifier_term_early", "G9", md_replace("In run 45074268881", "committed_never_processed In run 45074268881")),
        ("report_edit", "G7", {"committed": committed_edit}),
    ]


def self_test(raw, args):
    problems = 0
    try:
        pipeline(copy.deepcopy(raw), args)
        print("PASS real_inputs")
    except Fail as error:
        print(f"FAIL real_inputs: {error}")
        return 1
    for name, code, hooks in case_table():
        mode = "check" if code == "G7" else "render"
        committed = None
        if code == "G7":
            result = pipeline(copy.deepcopy(raw), args)
            committed = {"md": result["md"], "json": result["json"], "readme": result["row"]}
        try:
            pipeline(copy.deepcopy(raw), args, mode=mode, hooks=hooks, committed=committed)
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
    parser.add_argument("--mutation-run", required=True, help="workflow run ID of the passing mutation run")
    parser.add_argument("--mutation-artifact-id", required=True, help="artifact ID of its c5-summary artifact")
    parser.add_argument("--mutation-zip-sha256", required=True, help="SHA-256 of that artifact's ZIP")
    parser.add_argument("--mutation-summary-sha256", required=True, help="SHA-256 of its summary.json member")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--check", action="store_true")
    group.add_argument("--self-test", action="store_true")
    options = parser.parse_args()
    args = {"run": options.mutation_run, "artifact_id": options.mutation_artifact_id,
            "zip_sha256": options.mutation_zip_sha256, "summary_sha256": options.mutation_summary_sha256}
    try:
        expect(re.fullmatch(r"[0-9]{1,20}", args["run"]) is not None, "G1", "--mutation-run is not a decimal ID")
        expect(re.fullmatch(r"[0-9]{1,20}", args["artifact_id"]) is not None, "G1", "--mutation-artifact-id is not a decimal ID")
        raw = read_inputs(options.inputs)
        if options.self_test:
            return self_test(raw, args)
        if options.check:
            pipeline(raw, args, mode="check")
            print("PASS check")
            return 0
        result = pipeline(raw, args)
        write_outputs(result)
        print(result["row"])
        return 0
    except Fail as error:
        print(error)
        return 1


if __name__ == "__main__":
    sys.exit(main())

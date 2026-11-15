#!/usr/bin/env python3
"""Writes the B10 evidence pack and verdict inputs for scientific run 43689822806.

Run it from anywhere with `python3 -I`. The inputs are the artifacts and job
logs of the run, downloaded to one directory outside the repository (see INPUT_FILES
for the file names that directory must hold).

  make_results.py --inputs DIR               check everything, then write
                                             results/B10/2026-11-15.json and .md
                                             and README.md line 28, and print the row
  make_results.py --inputs DIR --check       re-render and require byte equality with
                                             the committed files
  make_results.py --inputs DIR --self-test   run the real inputs, then each single-change
                                             case; each must fail at its named check

Nothing is written unless every check G1 to G13 passes. A failure prints
`FAIL G<n> <reason>` and exits 1. The generator reads no network and holds no
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
from fractions import Fraction

ROOT = pathlib.Path(__file__).resolve().parents[4]
REPO = "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet"
RUN_ID_NUM = 43689822806
RUN_URL = f"{REPO}/actions/runs/{RUN_ID_NUM}"
RUN_ID = "b10-43689822806-1"
BASE = "2bb71e04c17f73d08c2fcb73e85165b787cf2c99"
DIAG = "dbc25c2005c3409ffd5f079c40b061d82c4b02aa"
MUTATION_COMMIT = "1e01aea24a457fffe0dc970703af2de608d524e3"
DATE = "2026-11-15"
JSON_PATH = "results/B10/2026-11-15.json"
MD_PATH = "results/B10/2026-11-15.md"
README_LINE = 28

EXPECTED_PATH = "scenarios/B10/expected.yaml"
SCENARIO_PATH = "scenarios/B10/scenario.yaml"
EXPECTED_SHA = "35c49404219eabe5be38479703817805c19401a091256a69bf64f5959beb0c1f"
SCENARIO_SHA = "762b1a540e8975129652358b0c91bb8a8c867b007dae162b00b78ad2dcc8569c"

# Input files: key -> (file name in the inputs directory, ZIP or log SHA-256, description).
INPUT_FILES = {
    "c0cand": ("b10-C0-candidate-43689822806-1.zip", "0017cda383d6b9c14b2519356d73329d7e6b38b70bf064159e4ccb27b2f2b7de"),
    "c1cand": ("b10-C1-candidate-43689822806-1.zip", "f31b157c30bdf3536e8b44dd482c1d252cf191cbb02c8a51a5b4f19acccb9c3d"),
    "c0pods": ("b10-C0-pods-43689822806-1.zip", "7072f2cbe9436156a52e69b5257ca9ffb19ef820fea3b0e2cca892aa5590bbde"),
    "c1pods": ("b10-C1-pods-43689822806-1.zip", "e804d6a5f600bb15a535afaf2afba5fe061575a8e8aa8211bad4f48ea583b582"),
    "c5": ("c5-summary-43670258319-1.zip", "8c0ce121878d714203bac000450b0052648b571c0a02a94cb378720a5278be75"),
    "c0log": ("job-132737914930.log", "ddf996cd9c86abd9fcb86159ec4c0092500a53b35b9f286b5b7d69acc8ce349f"),
    "c1log": ("job-132743948785.log", "1e13e925f62bd6608cc94b50387d7501777b067937a7f68fd952990f3dab82bf"),
}
ZIP_KEYS = ("c0cand", "c1cand", "c0pods", "c1pods", "c5")
MEMBER_PINS = {
    ("c0cand", "candidate.json"): "8bf46cd85488e567802f00e92202b8e4120b283b96842b95a4091d7bb1c9a40f",
    ("c1cand", "candidate.json"): "871a3b891544792e32cdba4fce0434216194a04086f3bfeb3ee0fad02f5be3f3",
    ("c5", "summary.json"): "d78dfef1ca86652632c7a6f2ec3c69e32749355df6455ee8ef78577fb1112e2f",
    ("c1pods", "b10-abrupt/r1/nonfinal-result.json"): "fc488ee46631fbecb046e5307be4421c3a8f6dd52fc7c89a82a4394ba5681726",
}
NONFINAL = ("c1pods", "b10-abrupt/r1/nonfinal-result.json")
# One digest over every member of each pods artifact: SHA-256 of the lines
# "<member path>\t<member SHA-256>\n", members sorted by path. It pins each Job's
# pods.json, collection-status.json and container log, and both phase-status.json files.
MANIFEST_PINS = {
    "c0pods": ("a05cfd5584af5c46aab1fd7a4d1e29bf4063d792305400a1cd19157332266eb3", 55),
    "c1pods": ("cbf06494b2ed60839c1b10229df6adfeb320a06b2aeea429fa5f3e80e30345fa", 240),
}
JOURNAL_PINS = {
    1: "b9f1d2202ba23a25086ca983c9632a233dbade485b9ee8729b5fcc5cf27e197c",
    2: "33963c4e0f8b3c96878f4e03bc63a102609033441ab888edace1c93d6cc07738",
    3: "67203a6c7503a825d214ee4dc59a69047b4085d9d00e71e2571a979a2242d947",
    4: "21d41c7485e16fd69101ebfc693ecff0edd1f57fa9f69f87a535c9d65faa6116",
    5: "d2a6c75ad5023f64653b1261eb71fa15c960c347112f8abff19ce68390988bd9",
}
ARTIFACTS = [
    ("c0cand", 14152258019, "b10-C0-candidate-43689822806-1"),
    ("c0pods", 14152018841, "b10-C0-pods-43689822806-1"),
    ("c1cand", 14155072618, "b10-C1-candidate-43689822806-1"),
    ("c1pods", 14154817943, "b10-C1-pods-43689822806-1"),
]
ARTIFACT_EXPIRY = "2027-02-13T07:24:40Z"
LOGS = [("c0log", 132737914930, "probe", "C0"), ("c1log", 132743948785, "full-C3", "C1")]

RESIDUAL = "A late append after the phase sweep line is not detected."
SUBJECT_RESTART = (
    "With idempotence enabled, application recovery that replaces a killed producer and "
    "resends a durably identified but unconfirmed logical message can leave both the "
    "original and the resend in the log."
)
TIMEOUT_RULE = {"complete_valid_final_repeats": 5, "duplicate_total": 56, "rule_selection": "confirmed"}
EXPECTED_PATH_LIST = [
    "infra/scripts/phase/phase.py",
    "infra/scripts/phase/tests/test_phase.py",
    "infra/scripts/phase/tests/test_workflow.py",
]
MERGE_484 = "26329ed64a6414a48526107566909675f6ee1c87"
MERGES_TEST_WORKFLOW = [
    "4aa6218f9c0750f80b072e0bab1e26e754de8f94",
    "57406da8e3964399ef7bf81351e5f57542fb6836",
    "3c4df3d040f55356597f83f1cf460956adb49197",
    "336f4e53f3f8329fae297c7438eef3e86df40120",
]
EXPECTED_ATTRIBUTION = {
    "infra/scripts/phase/phase.py": [MERGE_484],
    "infra/scripts/phase/tests/test_phase.py": [MERGE_484],
    "infra/scripts/phase/tests/test_workflow.py": MERGES_TEST_WORKFLOW,
}
COUNT_KEYS = ("ok", "loss", "phantom", "reported_failure", "duplicate", "reorder")
UNPUBLISHED_C1_REASON = (
    "earlier attempt of the same authorization; its C1 phase failed at the start-line Job of each "
    "of its five repeats, before any producer ran; nothing from it is published"
)

TEXT_ROOT_AND_RESTART = (
    "The C1 candidate's root status is invalid, with reasons b10_final_timeout_evidence_invalid and "
    "j7_counts_unavailable, only because repeat 1's timeout evidence is invalid: the verifier marks the "
    "root invalid when any repeat is invalid, and then leaves the root timeout totals absent. The restart "
    "verdict has its own rule, which the expected outcomes apply independently of the timeout verdict and "
    "of the root status: it needs at least three eligible repeats of five and a qualifying cross-restart "
    "pair. Each repeat's own timeout evidence counts only through its eligibility, which is why repeat 1 "
    "is ineligible. Four repeats are eligible and all four have such a pair, so the restart verdict is "
    "confirmed."
)
TEXT_JOURNAL = (
    "The statement that each same-instance resend came about 120 s after its original's confirmation comes "
    "from the raw restart journals (b10-abrupt/rN/*-loadgen-restart-*/journal.jsonl in the C1 pods artifact "
    "14154817943), not from a verifier output field. In each of the five repeats, the original's terminal "
    "record (delivered, error code 0, persistence status 2, Persisted) precedes the resend's prepared "
    "record by 119.995 to 119.997 s."
)
TEXT_REPEAT1 = (
    "Repeat 1 is invalid with reason b10_final_timeout_evidence_invalid, and its restart evidence is "
    "ineligible with reason b10_restart_timeout_bookkeeping_unavailable. Its retained nonfinal result "
    "(b10-abrupt/r1/nonfinal-result.json in the C1 pods artifact, SHA-256 "
    "fc488ee46631fbecb046e5307be4421c3a8f6dd52fc7c89a82a4394ba5681726, the file the final sweep bound) "
    "records problem b10_abrupt_verify_timeout_evidence_invalid with detail timeout_judgement, which the "
    "verifier raises when the timeout producer's evidence fails abrupt check 18: one of per-instance checks "
    "1 to 6 or 12 to 27, or the retry rule. The verifier does not record which; the timeout producer's "
    "stored evidence was destroyed with the ephemeral storage, and its pod log has no warning or error "
    "line. Its timeout counts are absent, not zero. Its restart arms are shown above for completeness and "
    "do not count toward the verdict."
)
TEXT_EXIT_CODES = (
    "Each C0 verify Job records outcome failed and exit code 1 by design: the scientific C0 verify handler "
    "exits 1 even when its window checks pass (docs/operations/verifier-contract.md, Scientific C0 verify). "
    "The C1 judge Job records outcome failed because the judge exits 1 for any root status other than "
    "complete_valid."
)
TEXT_CHANGES = (
    "Between the governed diagnostic run 43635822755 (main at dbc25c2005c3409ffd5f079c40b061d82c4b02aa) and "
    "this run, the only runtime change under src, charts, infra or the scenario workflow was pull request "
    "484, which raises whole-second verifier pod times before the B10 final sweep line in "
    "infra/scripts/phase/phase.py, with tests in infra/scripts/phase/tests/test_phase.py, and outside those "
    "paths changed docs/operations/b10-workflow.md; Hari decided it in decision 6884548840 on issue 483. "
    "Within those paths, pull requests 481, 485, 486 and 487 changed only "
    "infra/scripts/phase/tests/test_workflow.py, updating its pinned SHA-256 of the C5 mutation workflow; "
    "outside them they changed .github/workflows/verifier-c5-mutation.yml, files under "
    "tests/Parchwell.Verifier.Tests/Mutation, docs/operations/verifier-mutation.md and items/474/state.md. "
    "The diagnostic run is never published as a result."
)
TEXT_CHANGES_489 = (
    "Pull request 489 added tests under tests/Parchwell.Verifier.Tests/Component and changed "
    "docs/operations/b10-workflow.md; it changed no file under src, charts, infra or the scenario workflow."
)
TEXT_MUTATION = (
    "No file under src/Parchwell.Verifier changed between that commit and the run commit "
    "2bb71e04c17f73d08c2fcb73e85165b787cf2c99."
)
TEXT_UNPUBLISHED = (
    "This report uses only run 43689822806; nothing is published from run 43686555201, an earlier attempt "
    "of the same authorization whose C1 phase failed at the start-line Job of each of its five repeats, "
    "before any producer ran."
)
TEXT_BROKER = (
    "Per-repeat broker connection counts: unavailable. Scientific B10 candidates omit them; they are not zero."
)
TEXT_SIGKILL = (
    "The chain of termination evidence cannot exclude an external SIGKILL indistinguishable from the "
    "intended one, or a crash between kill_intent and the signal (ADR-021 protocol rule 9)."
)
TEXT_C1_COMPARISON = (
    "C1 timeout counts are a comparison only. Under idempotence, repeats 2 to 5 of C1 recorded 27 "
    "duplicates; this report draws no conclusion from them."
)
REQUIRED_SENTENCES = [
    "Microsoft's librdkafka configuration guidance has no message.timeout.ms or idempotence row.",
    "Duplicates appeared under timeout plus application resend: the C0 duplicate sum is 56. The verifier "
    "does not attribute a duplicate to the application resend rather than librdkafka's own retry.",
    RESIDUAL,
    "Workload data is synthetic.",
    "The restart is a process self-kill followed by pod replacement, with an unknown native tail, a fixed "
    "cut position at CutOrdinal 4, a fixed probe rate of 100 originals per second and a fixed arm order, "
    "same_instance then cross_restart; these are policy values, not measurements.",
    "The restart verdict claims neither pod or node loss nor survival of internal retry state.",
    TEXT_SIGKILL,
    TEXT_C1_COMPARISON,
    TEXT_ROOT_AND_RESTART,
    TEXT_JOURNAL,
]

TITLE = (
    "# B10: without idempotence, duplicates appeared under timeout plus application resend; with "
    "idempotence, a replacement producer's resend of an unconfirmed message still left both copies"
)
SUMMARY = (
    "In scientific run 43689822806 on Event Hubs Premium, with idempotence enabled and no transactions "
    "(C1), a fresh producer that replaced a self-killed one resent each message the killed producer had "
    "sent without receiving a delivery report, as a new `Produce` call with the same key and sequence "
    "number, and in each of the 4 eligible repeats of 5 both the original and the resend were in the log, "
    "so the restart recovery verdict is `confirmed` for the full subject below. "
    "In the same-instance control arm, the producer resent a message about 120 s after its delivery report "
    "had confirmed it as written, and both copies were in the log in every repeat too, so the comparison is "
    "`both` and the duplicate is not credited to the restart; that arm does not test a resend of an "
    "unconfirmed message without a restart. "
    "Without idempotence (C0), a `message.timeout.ms` of 100 and an application resend of each timed-out "
    "send left 56 duplicate records in five valid repeats (47, 9, 0, 0 and 0), so the timeout/application "
    "retry verdict is `confirmed`; the verifier does not attribute these duplicates between the "
    "application resend and librdkafka's own retry."
)
SUMMARY_SECOND = (
    " In the same-instance control arm, the producer resent a message about 120 s after its delivery "
    "report had confirmed it as written, and both copies were in the log in every repeat too, so the "
    "comparison is `both` and the duplicate is not credited to the restart; that arm does not test a "
    "resend of an unconfirmed message without a restart."
)
PLAIN_WORDS = (
    "In plain words: with idempotence on, when the producer was killed before a send was confirmed and its "
    "replacement resent that message, both copies were in the log, in each of the 4 eligible repeats of 5. "
    "In this test the same-instance resend came about 120 s after the original's successful confirmation, "
    "so it does not test a resend of an unconfirmed message without a restart. Tested on Event Hubs Premium "
    "with Confluent.Kafka 2.16.0 and librdkafka 2.16.0, without transactions, with five repeats per "
    "configuration and a synthetic workload."
)
REASONING = (
    "Reasoning, not measurement: idempotence only removes librdkafka's own internal retries. If your "
    "application calls Produce again for a message it thinks failed, you get a duplicate whenever the first "
    "one was actually written, and a timeout doesn't tell you whether it was. The first sentence is an "
    "inference, consistent with the expected outcomes' `c1_inference`. The second half of the second "
    "sentence has measured backing in C0 only, with idempotence off: 70 sends were reported failed but "
    "consumed (`phantom`), and 100 were reported failed and not consumed (`reported_failure`), in five "
    "repeats with a synthetic workload, and no rate is implied. librdkafka says of a send that timed out in "
    "flight that \"the producer can't know if the message reached the broker\" (INTRODUCTION.md v2.16.0)."
)
NOT_SHOWN = (
    "Not shown: pod or node loss; survival or loss of the producer's internal retry or idempotence state "
    "across the restart; whether an original was on the wire at the kill; and any rate, since there were "
    "five repeats per configuration with a synthetic workload and no measured rate is implied."
)
GUARANTEE = (
    "librdkafka v2.16.0 states that the idempotent producer guarantees that \"a message is only written to "
    "the log once\" (INTRODUCTION.md, Idempotent Producer, Guarantees). That this guarantee covers only "
    "librdkafka's own retries, and not a new application `Produce` call with the same key and sequence "
    "number, is not stated there; the expected outcomes label it an inference. In the same \"Idempotent "
    "Producer\" section, under \"Message persistence status\", librdkafka says of a message that \"was "
    "transmitted to the broker, but no acknowledgement was received\": \"Application retry will risk "
    "ordering and duplication.\" That text sits in the Idempotent Producer's error-handling section, which "
    "describes \"the Idempotent Producer specific errors\"; it does not say in words whether its retry "
    "warning holds with idempotence enabled. Of a message that was written and acknowledged, the same list "
    "says: \"No reason for application to retry.\" This run observed the duplicates; it did not test why."
)
POINTER = (
    "A pointer, not tested here: librdkafka's `transactional.id` \"is used to identify the same "
    "transactional producer instance across process restarts\" and lets the producer \"guarantee that "
    "transactions corresponding to earlier instances of the same producer have been finalized prior to "
    "starting any new transactions, and that any zombie instances are fenced off\" (CONFIGURATION.md "
    "v2.16.0). No cited source states that transactions prevent a duplicate created by an application "
    "resend; that is an inference. Microsoft's transactions guide says Kafka transactions are \"currently "
    "in Public preview in Premium, and Dedicated tier\", while its configuration page lists transactions as "
    "not currently supported (docs/blueprint.md section 12, conflict 6). B05 tested only that "
    "InitTransactions succeeds on Premium ([B05 result](../B05/2026-10-27.md)); begin, commit, abort, and "
    "whether transactions prevent this duplicate, were not tested."
)
PRACTICE = (
    "What this means in practice. This is the repository's guidance drawn from these results, not a "
    "Microsoft statement. With idempotence off, consumers must be prepared for duplicates, whatever their "
    "source. The source matters for which fix works. librdkafka documents idempotence as the guard against "
    "duplicates from its own retries: with it, \"a message is only written to the log once\", and a broker's "
    "duplicate-sequence error \"occurs upon retrying a previously successful send that was not "
    "acknowledged\" (INTRODUCTION.md v2.16.0). A duplicate from an application resend was not stopped by "
    "idempotence in this run. So enable idempotence to stop internal-retry duplicates. If the application "
    "itself resends unconfirmed messages, consumers must still de-duplicate. Transactions are a candidate, "
    "but no cited source states that they prevent this duplicate, and they are untested on Event Hubs; "
    "issue 493 proposes that test. C0's duplicates are not attributed between librdkafka's retries and the "
    "application resend, so this report does not say how many of them idempotence would have removed."
)
OPENING = [SUMMARY, PLAIN_WORDS, REASONING, NOT_SHOWN, GUARANTEE, POINTER, PRACTICE]
VERDICT_LINE_TIMEOUT = "Proposed verdict, timeout/application retry: confirmed."
VERDICT_LINE_RESTART = f"Proposed verdict, restart recovery: confirmed: {SUBJECT_RESTART}"
README_ROW = (
    "| B10 | Message timeout, application retry and restart recovery | With idempotence and no "
    "transactions, a replacement producer's resend of each message a self-killed producer had sent without "
    "confirmation left both copies in the log in each of 4 eligible repeats of 5. A same-producer resend of "
    "an already confirmed message also left both copies, so the duplicate is not credited to the restart. "
    "Without idempotence, with `message.timeout.ms=100` and an application resend of each timed-out send, "
    "five valid repeats left 56 duplicate records (47, 9, 0, 0, 0); the verifier cannot tell such a "
    "duplicate from one caused by librdkafka's own retry. Synthetic workload. | timeout/application retry: "
    f"confirmed; restart recovery: confirmed: {SUBJECT_RESTART} | [Report](results/B10/2026-11-15.md) |"
)


class Fail(Exception):
    def __init__(self, code, message):
        super().__init__(f"FAIL {code} {message}")
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
        fail("G12", "git " + " ".join(args) + " failed: " + result.stderr.decode("utf-8", "replace").strip())
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


def yaml_get(text, path):
    """Reads one scalar of expected.yaml by key path. Handles only plain nested mappings."""
    stack = []
    for line in text.splitlines():
        match = re.match(r"^( *)([A-Za-z_][A-Za-z0-9_./-]*):(?: (.*))?$", line)
        if not match:
            continue
        indent = len(match.group(1))
        while stack and stack[-1][0] >= indent:
            stack.pop()
        stack.append((indent, match.group(2)))
        value = match.group(3)
        if [key for _, key in stack] == path and value:
            return json.loads(value) if value.startswith('"') else value
    fail("G1", "expected.yaml has no scalar at " + ".".join(path))


def new_context(raw):
    ctx = Ctx()
    ctx.raw = raw
    ctx.members = {key: open_zip(raw[key], key) for key in ZIP_KEYS}
    ctx.expected_text = git("show", f"{BASE}:{EXPECTED_PATH}", binary=True)
    ctx.scenario_text = git("show", f"{BASE}:{SCENARIO_PATH}", binary=True)
    ctx.working_expected = (ROOT / EXPECTED_PATH).read_bytes()
    ctx.working_scenario = (ROOT / SCENARIO_PATH).read_bytes()
    ctx.g12_paths = list(EXPECTED_PATH_LIST)
    ctx.g12_attribution = copy.deepcopy(EXPECTED_ATTRIBUTION)
    return ctx


# ----------------------------------------------------------------------------------------------
# Checks


def manifest(members):
    digest = hashlib.sha256()
    for name in sorted(members):
        digest.update(f"{name}\t{sha256(members[name])}\n".encode())
    return digest.hexdigest(), len(members)


def journal_name(repeat, instance):
    return f"b10-abrupt/r{repeat}/{instance}/journal.jsonl"


def check_g1(ctx):
    for key, (name, pin) in INPUT_FILES.items():
        expect(sha256(ctx.raw[key]) == pin, "G1", f"{name} differs from its pinned SHA-256")
    for (key, member), pin in MEMBER_PINS.items():
        expect(member in ctx.members[key], "G1", f"{key} has no member {member}")
        expect(sha256(ctx.members[key][member]) == pin, "G1", f"member {member} of {key} differs from its pin")
    for key, (pin, count) in MANIFEST_PINS.items():
        digest, found = manifest(ctx.members[key])
        expect(found == count and digest == pin, "G1", f"the member manifest of {key} differs from its pin")
    expect(sha256(ctx.expected_text) == EXPECTED_SHA, "G1", "expected.yaml at the base commit differs from its pin")
    expect(sha256(ctx.scenario_text) == SCENARIO_SHA, "G1", "scenario.yaml at the base commit differs from its pin")
    for repeat, pin in JOURNAL_PINS.items():
        found = [n for n in ctx.members["c1pods"] if re.fullmatch(rf"b10-abrupt/r{repeat}/[^/]+-loadgen-restart-[^/]+/journal\.jsonl", n)]
        expect(len(found) == 1, "G1", f"repeat {repeat} has {len(found)} restart journals, expected 1")
        expect(sha256(ctx.members["c1pods"][found[0]]) == pin, "G1", f"journal of repeat {repeat} differs from its pin")


def strict_json(data, label):
    def no_constant(name):
        raise ValueError(name)

    try:
        return json.loads(data.decode("utf-8"), parse_constant=no_constant)
    except (ValueError, UnicodeDecodeError) as error:
        fail("G1", f"{label} is not strict JSON: {error}")


def parse(ctx):
    ctx.c0 = strict_json(ctx.members["c0cand"]["candidate.json"], "C0 candidate")
    ctx.c1 = strict_json(ctx.members["c1cand"]["candidate.json"], "C1 candidate")
    ctx.nonfinal = strict_json(ctx.members[NONFINAL[0]][NONFINAL[1]], "nonfinal result")
    ctx.summary = strict_json(ctx.members["c5"]["summary.json"], "C5 summary")
    ctx.phase = {
        "C0": strict_json(ctx.members["c0pods"]["phase-status.json"], "C0 phase status"),
        "C1": strict_json(ctx.members["c1pods"]["phase-status.json"], "C1 phase status"),
    }
    ctx.journals = {}
    for repeat in JOURNAL_PINS:
        name = [n for n in ctx.members["c1pods"] if re.fullmatch(rf"b10-abrupt/r{repeat}/[^/]+-loadgen-restart-[^/]+/journal\.jsonl", n)][0]
        lines = ctx.members["c1pods"][name].decode("utf-8").splitlines()
        ctx.journals[repeat] = [strict_json(line.encode("utf-8"), f"journal {repeat}") for line in lines if line.strip()]


def final_repeats(candidate, label, code):
    final = candidate.get("b10_final")
    expect(isinstance(final, dict), code, f"{label} has no b10_final")
    repeats = final.get("repeats")
    expect(isinstance(repeats, list) and [r.get("repeat") for r in repeats] == [1, 2, 3, 4, 5], code, f"{label} repeats are not 1 to 5")
    return final, repeats


def check_g2(ctx):
    c0 = ctx.c0
    expect(c0.get("scenario_id") == "B10" and c0.get("configuration_id") == "C0" and c0.get("run_id") == RUN_ID, "G2", "C0 candidate identity")
    expect(c0.get("source_commit") == BASE, "G2", "C0 candidate source commit")
    final, repeats = final_repeats(c0, "C0", "G2")
    expect(final.get("scan_complete") is True and final.get("residual_risk") == RESIDUAL, "G2", "C0 scan or residual risk")
    expect(final.get("restart_rule") is None, "G2", "C0 restart_rule is not null")
    expect(c0.get("status") == "complete_valid", "G2", "C0 root status is not complete_valid")
    for repeat in repeats:
        expect(repeat.get("status") == "complete_valid" and repeat.get("classification_final") is True, "G2", f"C0 repeat {repeat['repeat']} is not complete_valid and final")
        timeout = repeat.get("timeout")
        expect(isinstance(timeout, dict) and list(timeout) == list(COUNT_KEYS), "G2", f"C0 repeat {repeat['repeat']} timeout counts")
    total = sum(r["timeout"]["duplicate"] for r in repeats)
    expect(total == 56, "G2", f"C0 duplicate sum is {total}, expected 56")
    expect(final.get("timeout_rule") == TIMEOUT_RULE, "G2", "C0 timeout_rule differs from the pinned rule result")


def check_g3(ctx):
    _, repeats = final_repeats(ctx.c0, "C0", "G3")
    sums = {key: sum(r["timeout"][key] for r in repeats) for key in COUNT_KEYS}
    expect(ctx.c0.get("counts") == sums, "G3", "C0 root counts differ from the per-repeat sum")


def arm_of(repeat, name):
    arms = [a for a in repeat["restart"]["arms"] if a["arm"] == name]
    expect(len(arms) == 1, "G4", f"repeat {repeat['repeat']} has {len(arms)} {name} arms")
    return arms[0]


def recompute_restart_rule(repeats, expected_subject):
    eligible = [r["repeat"] for r in repeats if r["restart"]["eligibility"] == "eligible"]
    qualifying = []
    for repeat in repeats:
        cross = arm_of(repeat, "cross_restart")
        if repeat["repeat"] in eligible and cross["pair_count"] >= 1 and cross["unreported_seen"] >= 1:
            qualifying.append(repeat["repeat"])
    findings = [
        r["repeat"]
        for r in repeats
        if r["restart"]["termination"]["class"] != "no_kill" and any(a["lost_resends"] >= 1 for a in r["restart"]["arms"])
    ]
    eligible_loss = [n for n in findings if n in eligible]
    if len(eligible) < 3:
        selection, reason = "held", "fewer_than_three_eligible"
    elif qualifying:
        selection, reason = "confirmed", None
    elif eligible_loss:
        selection, reason = "held", "eligible_loss_without_pair"
    else:
        selection, reason = "untestable", "no_cross_restart_duplicate_observed"
    return {
        "declared_repeats": 5,
        "eligible_repeats": len(eligible),
        "eligible_repeat_numbers": eligible,
        "qualifying_pair_repeats": qualifying,
        "lost_resend_finding_repeats": findings,
        "eligible_loss_repeats": eligible_loss,
        "rule_selection": selection,
        "selection_reason": reason,
        "subject": expected_subject if selection == "confirmed" else None,
    }


def expected_subject(ctx):
    return yaml_get(ctx.expected_text.decode("utf-8"), ["verdict", "restart_recovery", "subject"])


def check_g4(ctx):
    c1 = ctx.c1
    expect(c1.get("scenario_id") == "B10" and c1.get("configuration_id") == "C1" and c1.get("run_id") == RUN_ID, "G4", "C1 candidate identity")
    expect(c1.get("source_commit") == BASE, "G4", "C1 candidate source commit")
    final, repeats = final_repeats(c1, "C1", "G4")
    expect(final.get("scan_complete") is True and final.get("residual_risk") == RESIDUAL, "G4", "C1 scan or residual risk")
    expect(final.get("timeout_rule") is None, "G4", "C1 timeout_rule is not null")
    subject = expected_subject(ctx)
    expect(subject == SUBJECT_RESTART, "G4", "the restart subject in expected.yaml differs from the pinned subject")
    recomputed = recompute_restart_rule(repeats, subject)
    expect(final.get("restart_rule") == recomputed, "G4", f"restart_rule differs from the recomputed rule: {recomputed}")
    expect(
        recomputed == {
            "declared_repeats": 5,
            "eligible_repeats": 4,
            "eligible_repeat_numbers": [2, 3, 4, 5],
            "qualifying_pair_repeats": [2, 3, 4, 5],
            "lost_resend_finding_repeats": [],
            "eligible_loss_repeats": [],
            "rule_selection": "confirmed",
            "selection_reason": None,
            "subject": SUBJECT_RESTART,
        },
        "G4",
        "the recomputed rule is not the pinned confirmed result",
    )
    for repeat in repeats:
        expect(repeat["restart"]["comparison"] == "both", "G4", f"repeat {repeat['repeat']} comparison is not both")
    one = repeats[0]
    expect(one["restart"]["eligibility"] == "ineligible" and one["restart"]["eligibility_reasons"] == ["b10_restart_timeout_bookkeeping_unavailable"], "G4", "repeat 1 eligibility")


def check_g5(ctx):
    one = ctx.c1["b10_final"]["repeats"][0]
    expect(one["status"] == "invalid" and one["reasons"] == ["b10_final_timeout_evidence_invalid"] and one["timeout"] is None, "G5", "C1 repeat 1 status, reasons or timeout")
    expect(one["source"]["sha256"] == MEMBER_PINS[NONFINAL], "G5", "repeat 1 source differs from the retained nonfinal file")
    data = ctx.nonfinal.get("data", {})
    expect(data.get("problems") == ["b10_abrupt_verify_timeout_evidence_invalid"], "G5", "nonfinal problems")
    expect(
        data.get("problem_details") == [{"problem": "b10_abrupt_verify_timeout_evidence_invalid", "detail": "timeout_judgement"}],
        "G5",
        "nonfinal problem_details",
    )


def check_g6(ctx):
    expect("counts" not in ctx.c1, "G6", "the C1 candidate has root counts")
    expect(ctx.c1.get("status") == "invalid", "G6", "C1 root status is not invalid")
    expect(ctx.c1.get("reasons") == ["b10_final_timeout_evidence_invalid", "j7_counts_unavailable"], "G6", "C1 root reasons")


def sum_counts(repeats):
    return {key: sum(r["timeout"][key] for r in repeats) for key in COUNT_KEYS}


def build_publication(ctx):
    c1_repeats = ctx.c1["b10_final"]["repeats"]
    for repeat in c1_repeats[1:]:
        expect(isinstance(repeat["timeout"], dict) and list(repeat["timeout"]) == list(COUNT_KEYS), "G6", f"C1 repeat {repeat['repeat']} timeout counts")
    text = ctx.expected_text.decode("utf-8")
    rule = ctx.c1["b10_final"]["restart_rule"]
    c0_repeats = ctx.c0["b10_final"]["repeats"]
    summary = ctx.summary
    inputs = [
        {"name": "C0 candidate artifact ZIP", "source": "artifact 14152258019, b10-C0-candidate-43689822806-1", "sha256": INPUT_FILES["c0cand"][1]},
        {"name": "C0 candidate.json", "source": "member candidate.json of artifact 14152258019", "sha256": MEMBER_PINS[("c0cand", "candidate.json")]},
        {"name": "C1 candidate artifact ZIP", "source": "artifact 14155072618, b10-C1-candidate-43689822806-1", "sha256": INPUT_FILES["c1cand"][1]},
        {"name": "C1 candidate.json", "source": "member candidate.json of artifact 14155072618", "sha256": MEMBER_PINS[("c1cand", "candidate.json")]},
        {"name": "C0 pods artifact ZIP", "source": "artifact 14152018841, b10-C0-pods-43689822806-1", "sha256": INPUT_FILES["c0pods"][1]},
        {"name": "C1 pods artifact ZIP", "source": "artifact 14154817943, b10-C1-pods-43689822806-1", "sha256": INPUT_FILES["c1pods"][1]},
        {"name": "C1 pods member b10-abrupt/r1/nonfinal-result.json", "source": "artifact 14154817943", "sha256": MEMBER_PINS[NONFINAL]},
        {"name": "C0 job log", "source": "job 132737914930 (probe)", "sha256": INPUT_FILES["c0log"][1]},
        {"name": "C1 job log", "source": "job 132743948785 (full-C3)", "sha256": INPUT_FILES["c1log"][1]},
        {"name": "C5 summary artifact ZIP", "source": "artifact 14145501472, c5-summary-43670258319-1", "sha256": INPUT_FILES["c5"][1]},
        {"name": "C5 summary.json", "source": "member summary.json of artifact 14145501472", "sha256": MEMBER_PINS[("c5", "summary.json")]},
        {"name": EXPECTED_PATH, "source": f"git show {BASE}:", "sha256": EXPECTED_SHA},
        {"name": SCENARIO_PATH, "source": f"git show {BASE}:", "sha256": SCENARIO_SHA},
    ]
    publication = {
        "item": "B10",
        "workload": "synthetic",
        "run": {
            "workflow_run_id": RUN_ID_NUM,
            "attempt": 1,
            "event": "workflow_dispatch",
            "ref": "main",
            "head_sha": BASE,
            "created_at": "2026-11-15T07:24:39Z",
            "conclusion": "success",
            "run_id": RUN_ID,
        },
        "expected_outcome": {"path": EXPECTED_PATH, "sha256": EXPECTED_SHA},
        "verdicts": {
            "timeout/application retry": {
                "proposed_verdict": "confirmed",
                "deciding_configuration": "C0",
                "rule": "docs/blueprint.md ADR-021, Restart decision rules, rule 1; scenarios/B10/expected.yaml verdict.timeout_application_retry.confirmed_when",
                "subject": yaml_get(text, ["verdict", "timeout_application_retry", "subject"]),
                "complete_valid_final_repeats": ctx.c0["b10_final"]["timeout_rule"]["complete_valid_final_repeats"],
                "duplicate_total": ctx.c0["b10_final"]["timeout_rule"]["duplicate_total"],
                "duplicates_by_repeat": [r["timeout"]["duplicate"] for r in c0_repeats],
                "attribution": yaml_get(text, ["hypotheses", "H_timeout_duplicate", "attribution"]),
            },
            "restart recovery": {
                "proposed_verdict": rule["rule_selection"],
                "deciding_configuration": "C1",
                "rule": "docs/blueprint.md ADR-021, Restart decision rules, rules 2 to 5; scenarios/B10/expected.yaml verdict.restart_recovery.confirmed_when",
                "subject": rule["subject"],
                "declared_repeats": rule["declared_repeats"],
                "eligible_repeats": rule["eligible_repeats"],
                "eligible_repeat_numbers": rule["eligible_repeat_numbers"],
                "ineligible_repeats": [
                    {"repeat": r["repeat"], "reasons": r["restart"]["eligibility_reasons"]}
                    for r in c1_repeats
                    if r["restart"]["eligibility"] != "eligible"
                ],
                "qualifying_pair_repeats": rule["qualifying_pair_repeats"],
                "lost_resend_finding_repeats": rule["lost_resend_finding_repeats"],
                "eligible_loss_repeats": rule["eligible_loss_repeats"],
                "comparison_by_repeat": {str(r["repeat"]): r["restart"]["comparison"] for r in c1_repeats},
                "c1_candidate_status": ctx.c1["status"],
                "c1_candidate_reasons": ctx.c1["reasons"],
            },
        },
        "c1_timeout_comparison": {
            "role": "comparison only; no verdict",
            "repeats_with_counts": [r["repeat"] for r in c1_repeats if r["timeout"] is not None],
            "repeat_1": "absent",
            "sum_repeats_2_to_5": sum_counts(c1_repeats[1:]),
        },
        "c1_repeat_1": {
            "status": c1_repeats[0]["status"],
            "reasons": c1_repeats[0]["reasons"],
            "nonfinal_problem": ctx.nonfinal["data"]["problems"][0],
            "nonfinal_detail": ctx.nonfinal["data"]["problem_details"][0]["detail"],
            "abrupt_check": 18,
            "failed_per_instance_check": "not_recorded",
        },
        "broker_connection_counts": "unavailable",
        "broker_connection_counts_reason": "scientific candidates omit broker_count (docs/operations/verifier-contract.md, Scientific B10 final judgement)",
        "environment": {
            "namespace_sku": "Premium",
            "processing_units": 1,
            "region": "uksouth",
            "node_size": "Standard_D4ds_v5",
            "node_size_source": "inferred from infra/cluster/aks.tf at the base commit; not captured",
        },
        "versions": ctx.c0["versions"],
        "mutation_acceptance": {
            "workflow_run_id": int(summary["run_id"]),
            "commit": summary["commit"],
            "measurement_complete": summary["measurement_complete"],
            "break_passed": summary["break_passed"],
            "summary_sha256": MEMBER_PINS[("c5", "summary.json")],
        },
        "unpublished_runs": [
            {"workflow_run_id": 43686555201, "reason": UNPUBLISHED_C1_REASON},
            {"workflow_run_id": 43635822755, "reason": "governed diagnostic run; never published as a result"},
        ],
        "changes_after_diagnostic_run": [
            {
                "pull_request": 484,
                "merge_commit": MERGE_484,
                "paths": ["infra/scripts/phase/phase.py", "infra/scripts/phase/tests/test_phase.py"],
                "kind": "implementation bug; paths lists the files within src, charts, infra and the scenario workflow; outside them it changed docs/operations/b10-workflow.md",
                "approval": "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/483#issuecomment-6884548840",
            },
            {
                "pull_requests": [481, 485, 486, 487],
                "merge_commits": MERGES_TEST_WORKFLOW,
                "paths": ["infra/scripts/phase/tests/test_workflow.py"],
                "kind": "within src, charts, infra and the scenario workflow, test only: pinned SHA-256 of .github/workflows/verifier-c5-mutation.yml; no B10 runtime effect",
            },
        ],
        "inputs": inputs,
    }
    return {"publication": publication, "candidates": {"C0": ctx.c0, "C1": ctx.c1}}


def check_g7(document):
    publication = document["publication"]
    expect(publication["broker_connection_counts"] == "unavailable", "G7", "broker connection counts are not marked unavailable")
    comparison = publication["c1_timeout_comparison"]
    expect(comparison["repeat_1"] == "absent" and 1 not in comparison["repeats_with_counts"], "G7", "C1 repeat 1 timeout counts are not absent")
    expect("counts" not in document["candidates"]["C1"], "G7", "C1 root counts are present")
    expect(publication["c1_repeat_1"]["failed_per_instance_check"] == "not_recorded", "G7", "the failed per-instance check is not marked not recorded")
    expect(publication["environment"]["node_size_source"].startswith("inferred"), "G7", "node size is not marked inferred")
    unavailable = {
        "broker_connection_counts": publication["broker_connection_counts"],
        "failed_per_instance_check": publication["c1_repeat_1"]["failed_per_instance_check"],
        "c1_timeout_comparison.repeat_1": comparison["repeat_1"],
    }
    for key, value in unavailable.items():
        expect(isinstance(value, str) and value not in ("", "0"), "G7", f"an unavailable measurement ({key}) is zero or empty")


def check_g7_report(md):
    """G7 on the rendered report: unavailable measurements are never shown as 0."""
    match = re.search(r"^## C1 timeout counts, comparison only\n(.*?)(?=^## |\Z)", md, re.S | re.M)
    expect(match is not None, "G7", "the C1 comparison section is missing")
    rows = [line for line in match.group(1).splitlines() if line.startswith("| 1 |")]
    expect(len(rows) == 1, "G7", "the C1 comparison section has no single repeat 1 row")
    cells = [cell.strip() for cell in rows[0].strip("|").split("|")]
    expect(cells[1:] == ["absent"] * 6, "G7", "C1 repeat 1 timeout counts are not all absent in the report")
    expect(TEXT_BROKER in md, "G7", "the broker connection count statement is missing")
    expect(not re.search(r"broker connection counts: 0", md, re.I), "G7", "the report shows broker connection counts as 0")


def check_g8(md):
    head, _, tail = md.partition("\n" + VERDICT_LINE_TIMEOUT)
    expect(tail != "", "G8", "the proposed-verdict lines are missing")
    expected_opening = TITLE + "\n\n" + "\n\n".join(OPENING)
    expect(head.startswith(expected_opening), "G8", "the opening of the report differs from the pinned text")
    expect(("\n" + VERDICT_LINE_RESTART) in tail, "G8", "the restart verdict line is missing")
    match = re.search(r"^## Statements required by the expected outcomes\n(.*?)(?=^## |\Z)", md, re.S | re.M)
    expect(match is not None, "G8", "the statements section is missing")
    for sentence in REQUIRED_SENTENCES:
        expect(sentence in match.group(1), "G8", f"required sentence missing: {sentence[:60]}")
    for sentence in (TEXT_UNPUBLISHED, TEXT_REPEAT1, TEXT_EXIT_CODES, TEXT_CHANGES, TEXT_CHANGES_489, TEXT_MUTATION, TEXT_BROKER, TEXT_ROOT_AND_RESTART, TEXT_JOURNAL):
        expect(sentence in md, "G8", f"pinned sentence missing: {sentence[:60]}")


def check_g10(ctx):
    expect(ctx.working_expected == ctx.expected_text, "G10", "working-tree expected.yaml differs from the base commit")
    expect(ctx.working_scenario == ctx.scenario_text, "G10", "working-tree scenario.yaml differs from the base commit")


def check_g11(*texts):
    for text in texts:
        expect(text.isascii(), "G11", "a rendered file has a byte outside ASCII")
        expect(not re.search(r"@@|\{\{|\}\}|<\.\.\.>", text), "G11", "a rendered file has a leftover template token")


def check_g12(ctx):
    listed = git("diff", "--name-only", DIAG, BASE, "--", "src", "charts", "infra", ".github/workflows/scenario.yml").split()
    expect(sorted(listed) == sorted(ctx.g12_paths), "G12", f"the changed paths {sorted(listed)} differ from the plan's list")
    for path, merges in ctx.g12_attribution.items():
        found = git("log", "--first-parent", "--format=%H", f"{DIAG}..{BASE}", "--", path).split()
        expect(sorted(found) == sorted(merges) and len(found) == len(merges), "G12", f"merge commits for {path} are {found}, not {merges}")


def check_g13(ctx):
    for repeat in ctx.c1["b10_final"]["repeats"]:
        number = repeat["repeat"]
        arm = arm_of(repeat, "same_instance")
        records = ctx.journals[number]
        cut_id = arm["cut_attempt_id"]
        resend_id = arm["witness"]["resend_attempt_id"]
        terminals = [r for r in records if r["kind"] == "terminal" and r["data"].get("attempt_id") == cut_id]
        prepared = [r for r in records if r["kind"] == "prepared" and r["data"].get("attempt_id") == resend_id]
        expect(len(terminals) == 1 and len(prepared) == 1, "G13", f"repeat {number}: terminal and resend prepared records are not unique")
        terminal, resend = terminals[0], prepared[0]
        data = terminal["data"]
        expect(data.get("delivered") is True and type(data.get("error_code")) is int and data["error_code"] == 0 and type(data.get("persistence_status")) is int and data["persistence_status"] == 2, "G13", f"repeat {number}: the original's terminal record is not delivered, error 0, persistence status 2")
        expect(terminal["sequence"] < resend["sequence"], "G13", f"repeat {number}: the terminal record does not precede the resend's prepared record")
        expect(terminal["clock_frequency"] == resend["clock_frequency"] and terminal["clock_frequency"] > 0, "G13", f"repeat {number}: clock frequencies differ")
        gap = Fraction(resend["monotonic_ticks"] - terminal["monotonic_ticks"], terminal["clock_frequency"])
        expect(Fraction(119995, 1000) <= gap <= Fraction(119997, 1000), "G13", f"repeat {number}: gap {float(gap):.6f} s is outside 119.995 to 119.997 s")


def journal_gaps(ctx):
    gaps = {}
    for repeat in ctx.c1["b10_final"]["repeats"]:
        arm = arm_of(repeat, "same_instance")
        records = ctx.journals[repeat["repeat"]]
        terminal = [r for r in records if r["kind"] == "terminal" and r["data"].get("attempt_id") == arm["cut_attempt_id"]][0]
        resend = [r for r in records if r["kind"] == "prepared" and r["data"].get("attempt_id") == arm["witness"]["resend_attempt_id"]][0]
        gaps[repeat["repeat"]] = float(Fraction(resend["monotonic_ticks"] - terminal["monotonic_ticks"], terminal["clock_frequency"]))
    return gaps


# ----------------------------------------------------------------------------------------------
# Rendering


def table(headers, rows):
    lines = ["| " + " | ".join(headers) + " |", "|" + "|".join("---" for _ in headers) + "|"]
    for row in rows:
        lines.append("| " + " | ".join(str(cell) for cell in row) + " |")
    return "\n".join(lines)


def join_list(values):
    return ", ".join(str(v) for v in values) if values else "none"


def exit_codes(ctx, phase, members):
    lifecycle = ctx.phase[phase]["b10_lifecycle"]
    known = {item["job"]: item["exit_code"] for group in ("producers", "verifiers") for item in lifecycle.get(group, [])}
    rows = []
    for mode in ctx.phase[phase]["modes"]:
        job = mode["job"]
        if job in known:
            code = known[job]
        else:
            pods = strict_json(members[f"{job}/pods.json"], f"{job} pods.json")
            code = pods["items"][0]["status"]["containerStatuses"][0]["state"]["terminated"]["exitCode"]
        repeat = mode.get("repeat")
        rows.append([phase, job, "n/a" if repeat is None else repeat, mode["mode"], mode["outcome"], code])
    return rows


def render_md(ctx, document):
    publication = document["publication"]
    c0, c1 = ctx.c0, ctx.c1
    c0_repeats = c0["b10_final"]["repeats"]
    c1_repeats = c1["b10_final"]["repeats"]
    rule = c1["b10_final"]["restart_rule"]
    blob = f"{REPO}/blob/{BASE}/"
    out = [TITLE, ""]
    for paragraph in OPENING:
        out += [paragraph, ""]
    out += [VERDICT_LINE_TIMEOUT, "", VERDICT_LINE_RESTART, ""]

    out += ["## Approved inputs and run", ""]
    out += [
        f"- Scenario and expected outcome as approved: [scenarios/B10/scenario.yaml]({blob}{SCENARIO_PATH}) and [scenarios/B10/expected.yaml]({blob}{EXPECTED_PATH}) at `{BASE}`. The expected outcomes' SHA-256 is `{EXPECTED_SHA}`.",
        f"- Run: [{RUN_ID_NUM}]({RUN_URL}), attempt 1, event `workflow_dispatch`, ref `main`, head `{BASE}`, created 2026-11-15T07:24:39Z, conclusion `success`.",
        f"- Dispatch authorization: [decision 6890385963](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6890385963) and its [repeat 6891036629](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6891036629).",
    ]
    out += ["- Artifacts (expire " + ARTIFACT_EXPIRY + "):"]
    for key, artifact_id, name in ARTIFACTS:
        out += [f"  - [{artifact_id}]({REPO}/actions/runs/{RUN_ID_NUM}/artifacts/{artifact_id}), `{name}`, ZIP SHA-256 `{INPUT_FILES[key][1]}`."]
    out += ["- Job logs:"]
    for key, job_id, job_key, phase in LOGS:
        out += [f"  - [{job_id}]({REPO}/actions/runs/{RUN_ID_NUM}/job/{job_id}), workflow job `{job_key}` ({phase}), SHA-256 `{INPUT_FILES[key][1]}`."]
    out += [
        "- Generator and receipt: [make_results.py](../../docs/operations/evidence/issue-492-b10-verdict/make_results.py) and [receipt.md](../../docs/operations/evidence/issue-492-b10-verdict/receipt.md).",
        "",
        TEXT_UNPUBLISHED,
        "",
    ]

    out += ["## Timeout/application retry (C0)", ""]
    rows = [[r["repeat"], r["status"], str(r["classification_final"]).lower()] + [r["timeout"][k] for k in COUNT_KEYS] for r in c0_repeats]
    totals = sum_counts(c0_repeats)
    rows.append(["Total", "all", "all"] + [totals[k] for k in COUNT_KEYS])
    out += [table(["Repeat", "Status", "classification_final"] + list(COUNT_KEYS), rows), ""]
    out += [
        "The rule is ADR-021 Restart decision rules, rule 1: the verdict is `confirmed` when all five repeats are `complete_valid` with `classification_final` true and the checked sum of their duplicate counts is at least 1. "
        f"Here the sum is {totals['duplicate']} in {c0['b10_final']['timeout_rule']['complete_valid_final_repeats']} valid final repeats, so the selection is `{c0['b10_final']['timeout_rule']['rule_selection']}`. "
        "The verifier counts duplicates by logical key and sequence within the timeout cohort, so a duplicate from the application resend and one from librdkafka's own retry cannot be told apart (`H_timeout_duplicate.attribution`).",
        "",
    ]

    out += ["## Restart recovery (C1)", ""]
    rows = []
    for r in c1_repeats:
        restart = r["restart"]
        native = restart["native_errors"]
        rows.append([
            r["repeat"], r["status"], restart["eligibility"], join_list(restart["eligibility_reasons"]), restart["comparison"],
            restart["termination"]["class"], f"{native['count']}, {native['fatal_count']}, {native['tail']}",
        ])
    out += [table(["Repeat", "Status", "Eligibility", "Reasons", "Comparison", "Termination", "Native errors (count, fatal, tail)"], rows), ""]
    arm_headers = ["Repeat", "Arm", "Cut attempt", "Unconfirmed", "Resends invoked", "Originals seen", "Resends seen", "Pairs", "Unreported attempts", "Unreported seen", "Unreported unseen", "Lost resends"]
    rows, witnesses = [], []
    for r in c1_repeats:
        for arm in r["restart"]["arms"]:
            rows.append([
                r["repeat"], arm["arm"], arm["cut_attempt_id"], arm["unconfirmed_count"], arm["resend_invoked"], arm["originals_seen"],
                arm["resends_seen"], arm["pair_count"], arm["unreported_attempts"], arm["unreported_seen"], arm["unreported_unseen"], arm["lost_resends"],
            ])
            w = arm["witness"]
            witnesses.append([
                r["repeat"], arm["arm"], w["original_attempt_id"], w["resend_attempt_id"],
                f"({w['original']['partition']}, {w['original']['offset']})", f"({w['resend']['partition']}, {w['resend']['offset']})",
            ])
    out += [table(arm_headers, rows), ""]
    out += [table(["Repeat", "Arm", "Original attempt", "Resend attempt", "Original (partition, offset)", "Resend (partition, offset)"], witnesses), ""]
    ineligible = ", ".join(f"repeat {i['repeat']}: {', '.join(i['reasons'])}" for i in publication["verdicts"]["restart recovery"]["ineligible_repeats"])
    out += [
        f"- `declared_repeats`: {rule['declared_repeats']}",
        f"- `eligible_repeats`: {rule['eligible_repeats']} (repeats {join_list(rule['eligible_repeat_numbers'])})",
        f"- Ineligible: {ineligible}",
        f"- `qualifying_pair_repeats`: {join_list(rule['qualifying_pair_repeats'])}",
        f"- `lost_resend_finding_repeats`: {join_list(rule['lost_resend_finding_repeats'])}",
        f"- `eligible_loss_repeats`: {join_list(rule['eligible_loss_repeats'])}",
        f"- `rule_selection`: `{rule['rule_selection']}`; `selection_reason`: null",
        f"- `subject`: {rule['subject']}",
        "",
        TEXT_ROOT_AND_RESTART,
        "",
        TEXT_JOURNAL,
        "",
    ]

    out += ["## C1 timeout counts, comparison only", ""]
    rows = []
    for r in c1_repeats:
        rows.append([r["repeat"]] + (["absent"] * 6 if r["timeout"] is None else [r["timeout"][k] for k in COUNT_KEYS]))
    sums = publication["c1_timeout_comparison"]["sum_repeats_2_to_5"]
    rows.append(["Sum, repeats 2 to 5"] + [sums[k] for k in COUNT_KEYS])
    out += [table(["Repeat"] + list(COUNT_KEYS), rows), ""]

    out += ["## C1 repeat 1", "", TEXT_REPEAT1, ""]

    out += ["## Versions, environment and timing", ""]
    rows = []
    for phase, candidate in (("C0", c0), ("C1", c1)):
        v = candidate["versions"]
        d = candidate["image_digests"]
        t = candidate["timings"]
        rows.append([phase, v["confluent_kafka"], v["librdkafka"], v["dotnet"], v["verifier"], v["workload_generator"], d["loadgen"], d["verifier"], t["worker_started_utc"], t["judge_completed_utc"], t["duration_seconds"]])
    out += [table(["Phase", "Confluent.Kafka", "librdkafka", ".NET", "Verifier", "Workload generator", "Loadgen image", "Verifier image", "Worker started", "Judge completed", "Duration (s)"], rows), ""]
    out += [
        f"- Event Hubs namespace: SKU Premium, 1 processing unit, region uksouth, from the Terraform plan lines of the job logs (C0 log lines 1378, 1387 and 1393; C1 log lines 1206, 1215 and 1221). The cluster plan reports `No changes.` at C0 log line 886 and C1 log line 719.",
        "- AKS node size Standard_D4ds_v5 is inferred from `infra/cluster/aks.tf` at the base commit (lines 35, 44 and 51), not captured from the run.",
        "- Teardown: `ephemeral_state_empty` at 2026-11-15T07:51:50.8422656Z (C0 log line 3610) and at 2026-11-15T09:06:36.3740584Z (C1 log line 4262); `aks_stopped` at 2026-11-15T09:07:11.4252371Z (C1 log line 4321).",
        "",
        TEXT_BROKER,
        "",
    ]

    out += ["## Pod evidence", ""]
    rows = exit_codes(ctx, "C0", ctx.members["c0pods"]) + exit_codes(ctx, "C1", ctx.members["c1pods"])
    out += [table(["Phase", "Job", "Repeat", "Mode", "Outcome", "Exit code"], rows), ""]
    out += [TEXT_EXIT_CODES, ""]
    out += [f"The artifacts are [{ARTIFACTS[1][1]}]({REPO}/actions/runs/{RUN_ID_NUM}/artifacts/{ARTIFACTS[1][1]}) (C0 pods) and [{ARTIFACTS[3][1]}]({REPO}/actions/runs/{RUN_ID_NUM}/artifacts/{ARTIFACTS[3][1]}) (C1 pods).", ""]

    out += ["## Changes after the diagnostic run", "", TEXT_CHANGES, "", TEXT_CHANGES_489, ""]

    mutation = publication["mutation_acceptance"]
    out += [
        "## Mutation acceptance",
        "",
        f"C5 mutation run [{mutation['workflow_run_id']}]({REPO}/actions/runs/{mutation['workflow_run_id']}) on `{mutation['commit']}`: `measurement_complete` {str(mutation['measurement_complete']).lower()}, `break_passed` {str(mutation['break_passed']).lower()}, summary SHA-256 `{mutation['summary_sha256']}`.",
        "",
        TEXT_MUTATION,
        "",
    ]

    out += ["## Statements required by the expected outcomes", ""]
    out += [f"- {sentence}" for sentence in REQUIRED_SENTENCES]
    out += [""]

    text = ctx.expected_text.decode("utf-8")
    out += ["## Not claimed", ""]
    for key in ("internal_retry_state", "pod_or_node_loss", "wire_inflight_state", "universal_causation"):
        out += [f"- {yaml_get(text, ['not_claimed', key])}"]
    out += [f"- {TEXT_SIGKILL}", ""]
    return "\n".join(out).rstrip("\n") + "\n"


# ----------------------------------------------------------------------------------------------
# Pipeline


def pipeline(raw, mode="write", hooks=None, committed=None):
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
    document = build_publication(ctx)
    hook("publication", document)
    check_g7(document)
    md_box = [render_md(ctx, document)]
    hook("md", md_box)
    md = md_box[0]
    check_g7_report(md)
    check_g8(md)
    json_text = json.dumps(document, indent=2, ensure_ascii=True) + "\n"
    check_g11(md, json_text, README_ROW)
    check_g10(ctx)
    hook("g12", ctx)
    check_g12(ctx)
    check_g13(ctx)
    result = {"md": md, "json": json_text, "row": README_ROW, "ctx": ctx}
    if mode == "check":
        files = committed if committed is not None else read_committed()
        hook("committed", files)
        expect(files["md"] == md, "G9", f"{MD_PATH} differs from the re-rendered report")
        expect(files["json"] == json_text, "G9", f"{JSON_PATH} differs from the re-rendered JSON")
        expect(files["readme"] == README_ROW, "G9", f"README.md line {README_LINE} differs from the re-rendered row")
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
    expect(lines[README_LINE - 1].startswith("| B10 |"), "G9", f"README.md line {README_LINE} is not the B10 row")
    (ROOT / "results" / "B10").mkdir(parents=True, exist_ok=True)
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
    def members(key, name):
        return lambda ctx: ctx.members[key].__setitem__(name, flip(ctx.members[key][name]))

    def parsed(fn):
        return {"parsed": fn}

    def c1_repeat(ctx, number):
        return ctx.c1["b10_final"]["repeats"][number - 1]

    def set_ineligible(ctx, numbers):
        for number in numbers:
            c1_repeat(ctx, number)["restart"]["eligibility"] = "ineligible"

    def journal_records(ctx, number, kind, attempt_suffix):
        return [r for r in ctx.journals[number] if r["kind"] == kind and r["data"].get("attempt_id", "").endswith(attempt_suffix)]

    def md_replace(old, new):
        def apply(box):
            expect(old in box[0], "SELFTEST", f"case text not found: {old[:40]}")
            box[0] = box[0].replace(old, new)

        return {"md": apply}

    def journal_order(ctx):
        journal_records(ctx, 2, "terminal", "same_instance-4")[0]["data"]["persistence_status"] = 1

    def journal_sequence(ctx):
        journal_records(ctx, 2, "prepared", "same_instance-5")[0]["sequence"] = 19

    def journal_gap_low(ctx):
        terminal = journal_records(ctx, 2, "terminal", "same_instance-4")[0]
        journal_records(ctx, 2, "prepared", "same_instance-5")[0]["monotonic_ticks"] = terminal["monotonic_ticks"] + 119_994_000_000

    def lost_resend(ctx):
        arm_of(c1_repeat(ctx, 1), "cross_restart")["lost_resends"] = 1

    def pair(ctx):
        arm_of(c1_repeat(ctx, 3), "cross_restart")["unreported_seen"] = 0

    def subject(ctx):
        ctx.c1["b10_final"]["restart_rule"]["subject"] = ctx.c1["b10_final"]["restart_rule"]["subject"].rsplit(" ", 1)[0]

    def broker_zero(document):
        document["publication"]["broker_connection_counts"] = 0

    def committed_edit(files):
        expect("154978" in files["md"], "SELFTEST", "committed report has no 154978 cell")
        files["md"] = files["md"].replace("154978", "154977", 1)

    def drop_expected_path(ctx):
        ctx.g12_paths = [p for p in ctx.g12_paths if not p.endswith("test_workflow.py")]

    def wrong_attribution(ctx):
        ctx.g12_attribution["infra/scripts/phase/tests/test_workflow.py"] = [MERGE_484]

    return [
        ("hash_c0_candidate", "G1", {"members": members("c0cand", "candidate.json")}, "write"),
        ("hash_nonfinal_r1", "G1", {"members": members(*NONFINAL)}, "write"),
        ("c0_duplicate_r1", "G2", parsed(lambda ctx: ctx.c0["b10_final"]["repeats"][0]["timeout"].__setitem__("duplicate", 46)), "write"),
        ("c0_repeat_status", "G2", parsed(lambda ctx: ctx.c0["b10_final"]["repeats"][4].__setitem__("status", "invalid")), "write"),
        ("c0_root_ok", "G3", parsed(lambda ctx: ctx.c0["counts"].__setitem__("ok", ctx.c0["counts"]["ok"] - 1)), "write"),
        ("c1_eligibility_r2", "G4", parsed(lambda ctx: set_ineligible(ctx, [2])), "write"),
        ("c1_pair_r3", "G4", parsed(pair), "write"),
        ("c1_lost_resend_r1", "G4", parsed(lost_resend), "write"),
        ("c1_subject", "G4", parsed(subject), "write"),
        ("c1_selection_boundary", "G4", parsed(lambda ctx: set_ineligible(ctx, [2, 3])), "write"),
        ("nonfinal_detail", "G5", parsed(lambda ctx: ctx.nonfinal["data"]["problem_details"][0].__setitem__("detail", "timeout_evidence")), "write"),
        ("c1_root_counts_present", "G6", parsed(lambda ctx: ctx.c1.__setitem__("counts", {k: 0 for k in COUNT_KEYS})), "write"),
        ("broker_zero", "G7", {"publication": broker_zero}, "write"),
        ("missing_sentence", "G8", md_replace(RESIDUAL, ""), "write"),
        ("missing_opening", "G8", md_replace(SUMMARY_SECOND, ""), "write"),
        ("report_edit", "G9", {"committed": committed_edit}, "check"),
        ("expected_yaml_edit", "G10", {"working": lambda ctx: setattr(ctx, "working_expected", flip(ctx.working_expected))}, "write"),
        ("non_ascii", "G11", md_replace("## Pod evidence", "## Pod evidence " + chr(0x2013)), "write"),
        ("extra_change", "G12", {"g12": drop_expected_path}, "write"),
        ("wrong_attribution", "G12", {"g12": wrong_attribution}, "write"),
        ("journal_order", "G13", parsed(journal_order), "write"),
        ("journal_sequence", "G13", parsed(journal_sequence), "write"),
        ("journal_gap_low", "G13", parsed(journal_gap_low), "write"),
    ]


def self_test(raw):
    problems = 0
    try:
        pipeline(copy.deepcopy(raw), mode="check")
        print("PASS real_inputs")
    except Fail as error:
        print(f"FAIL real_inputs: {error}")
        return 1
    for name, code, hooks, mode in case_table():
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

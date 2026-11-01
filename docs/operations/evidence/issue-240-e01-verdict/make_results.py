"""Assemble the B08 or E01 publication from scientific run 41292426657, offline.

Supported inputs are the extracted candidate and public pod artifacts of attempt
1, the C5 summary of run 41232286130, and this checkout's git objects for the two
commits named below. Run from any working directory:

    python3 -I docs/operations/evidence/<this directory>/make_results.py \
        --item B08|E01 --candidates DIR --pods DIR --c5-summary FILE \
        [--check] [--tables]

Only results/<ITEM>/2026-11-01.json is written. --check compares its exact bytes
and also requires the report results/<ITEM>/2026-11-01.md to contain the tables
that --tables prints, so no table row is typed by hand. This follows the issue-210
and issue-230 publication assembler precedent. It is not a second scientific
validator: the verifier's complete-valid projections stay authoritative, and the
the candidate of each configuration (five repeats) and its judge receipt are preserved
unchanged, re-serialized with indentation. The closed decision rules below are
transcribed from the pinned scenarios/<ITEM>/expected.yaml and are only used to
confirm that the verifier's proposed assessment follows from the candidates.

Failure conditions: a pinned input hash differs or an input is missing; candidate
identity, status, determinism, outcome class, diagnostics or repeat projection
differ from the expected shape; a judge receipt does not bind the candidate hash
or its actual pod name and UID; a Job pod is missing, duplicated, restarted,
non-zero, on another image digest or its collection has gaps; phase-status.json
does not show the item selected, every Job complete and every candidate accepted
and evidence-valid; a pod-log capture line disagrees with the candidate on
delivery, read record, key, value, settings or partition metadata; the derived
assessment differs from the verifier's; the B08 independent partition arithmetic
differs from the delivered partitions; the C5 summary is not complete and passing
or its totals do not add up; the six configured C5 targets are not compared
between the two commits; or --check output differs.

Only approved projections and provenance are read from pod JSON and captures; raw
pod documents, token-exchange log lines and statistics are never copied.
Regression example: copy the candidates directory, append a newline to
B08-C0.json and use it with --check: the pinned SHA-256 check fails before any
result can be written. The original inputs stay intact.
"""

import argparse
import base64
import hashlib
import json
import pathlib
import subprocess
import sys
import zlib

ROOT = pathlib.Path(__file__).resolve().parents[4]
REPO = "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet"
RUN = REPO + "/actions/runs/41292426657"
RUN_ID = "probe-41292426657-1"
SOURCE = "8078e6fcc16b1115899fbfb3edfbb1299cb8eaf1"
CANDIDATE_ARTIFACT = RUN + "/artifacts/13078662873"
POD_ARTIFACT = RUN + "/artifacts/13078313354"
BATCH_JOB = RUN + "/job/124806233585"
STOP_JOB = RUN + "/job/124815068060"
C5_RUN = REPO + "/actions/runs/41232286130"
C5_SUMMARY_ARTIFACT = C5_RUN + "/artifacts/13057507952"
C5_COMMIT = "2dc9715e3de176f7e1cd55eee9c251e43c29c746"
C5_CONFIG = "tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json"
DATE = "2026-11-01"
SUMMARY_WORD = {"B08": "partition", "E01": "tombstone"}

PINS = {
    "B08": {
        "candidates": {
            "B08-C0-judge-completion.json": "f06594ddf1feb0baaf5c151d2fa892c8e41ac46906d705f9d69ee43c77df3a75",
            "B08-C0.json": "8163b08b3bbf1a0cc435755daa03107a4f7ff5d6492c6ca191b422eaea4986ef",
            "B08-C1-judge-completion.json": "f8013261d4ecb754009c39fa55156239ecd3f9485cdd8645505ebf302b7f00f6",
            "B08-C1.json": "0538240056f4f695cf8c5542275bd9ab4e5754be46377ba9e3ce2d1c5c5aef99"
        },
        "pods": {
            "probe-41292426657-1-b08-c0-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-b08-c0-action/pods.json": "0025617eda59c7cdeb46218feaceade22910c82b4117976b732bc9fc771d59a3",
            "probe-41292426657-1-b08-c0-action/probe-41292426657-1-b08-c0-action-dwpsg.containers.loadgen.log": "4b67cd927c2bb915654f31c33f5841d62e8753c5912ac64d8fd4a74b076be34a",
            "probe-41292426657-1-b08-c0-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-b08-c0-judge/pods.json": "0e762a8f448735b9c6dad49e497320f44924f790e383b33f11657b453e066751",
            "probe-41292426657-1-b08-c0-judge/probe-41292426657-1-b08-c0-judge-4s486.containers.verifier.log": "d9e5a52332baa269c27eca03506cf6951db55557b55ebb901d5df39337227167",
            "probe-41292426657-1-b08-c0-read/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-b08-c0-read/pods.json": "f4a83aeffe4a22a77b8f3d8fb4ed0307165e91626fd83ee466dd84eeffb123ff",
            "probe-41292426657-1-b08-c0-read/probe-41292426657-1-b08-c0-read-zffsv.containers.probe.log": "dab2aac2fb5a48f9d0e1bf990a8e617e7cae7e53d85eaa14e0fe883c92cc7797",
            "probe-41292426657-1-b08-c1-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-b08-c1-action/pods.json": "71ecd8e09a280ad7fc39694a5777a2a24cbe16fb3ad68d644472403a726e80a3",
            "probe-41292426657-1-b08-c1-action/probe-41292426657-1-b08-c1-action-4th4g.containers.loadgen.log": "a89acbfb827ef444f3bab4a994bd0f00d5d11d2648a9aaa808756bb030591266",
            "probe-41292426657-1-b08-c1-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-b08-c1-judge/pods.json": "f513bc96ea3ca591e0c96bca7f5f0a6957e5ebc03fdd5d82be4a2350e3e2afd6",
            "probe-41292426657-1-b08-c1-judge/probe-41292426657-1-b08-c1-judge-vd7zq.containers.verifier.log": "9513c6947a4d8c56d7f252bf2a64586e15af6a4c24cc6caec3ea14b46f254162",
            "probe-41292426657-1-b08-c1-read/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-b08-c1-read/pods.json": "0eb3dbf61add2bb0ff64d63e809ad6709e21565d62c02030da0321a0bbddb000",
            "probe-41292426657-1-b08-c1-read/probe-41292426657-1-b08-c1-read-jx7g4.containers.probe.log": "dd1bba9b6627198015ebca71fe3f5f731564f1b970beec3d64ddd2507e05b042"
        }
    },
    "E01": {
        "candidates": {
            "E01-C0-judge-completion.json": "274ba6782bd1140725cd2dd7598d46a835e3958af631e2f1c0763efedd46a342",
            "E01-C0.json": "92fef6697c792fe522b7360c44039e317381f79b09b5f57813c0aa5976ba2298",
            "E01-C1-judge-completion.json": "5beb99931123b2abd1ddf0121e1cf1a427a9636354231fc42594f4195ccceded",
            "E01-C1.json": "e44cbaeb567b553db59b9abce8ad12d57ec7166d9296dee167e492370a3a21c6"
        },
        "pods": {
            "probe-41292426657-1-e01-c0-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-e01-c0-action/pods.json": "df1644fdbfc3d3bbc67614dc1fa3b947b2a27f5a0acc338766d2c4f29a4b46ec",
            "probe-41292426657-1-e01-c0-action/probe-41292426657-1-e01-c0-action-nd4dm.containers.probe.log": "80905288ecca5c0bf6789084b840f9f9ad14abbb32d9dd9e7db901583c8a337d",
            "probe-41292426657-1-e01-c0-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-e01-c0-judge/pods.json": "a8b2d0459cb8201572bc4dc713376576b6ef307868f15d7af9b76d068b0874a1",
            "probe-41292426657-1-e01-c0-judge/probe-41292426657-1-e01-c0-judge-v4g2w.containers.verifier.log": "301b175d7cdb1bf7908cca5d3d14fa8732484f520825d0cc1d046d2549af6738",
            "probe-41292426657-1-e01-c0-read/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-e01-c0-read/pods.json": "0e7fbbcc452b4178cfdcbcfd0dac6d207588583db567dbb72a662419dce78183",
            "probe-41292426657-1-e01-c0-read/probe-41292426657-1-e01-c0-read-897np.containers.probe.log": "f321a5d9c68ac499b5c4a328b5115a0ad8244b37899aec5977099a54de0d3446",
            "probe-41292426657-1-e01-c0-seed/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-e01-c0-seed/pods.json": "2bf553acbbcaa17abd63a1f2de4d5c5241b01d5265b8399d35b8bae1899056e0",
            "probe-41292426657-1-e01-c0-seed/probe-41292426657-1-e01-c0-seed-clqgx.containers.enrichment-consumer.log": "27caf19ab607212e40f5da1083c7bf64336daa18540748d93eb0204de8732365",
            "probe-41292426657-1-e01-c0-seedread/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-e01-c0-seedread/pods.json": "bb13655027193912eb804c638343d12a06487b7da9884168303a0a9298340256",
            "probe-41292426657-1-e01-c0-seedread/probe-41292426657-1-e01-c0-seedread-t5zcv.containers.probe.log": "a3fdc7f03d28fc83a631d941aa73a50fa7fccdd9793a10dfda97b50be024d56b",
            "probe-41292426657-1-e01-c1-action/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-e01-c1-action/pods.json": "f0d0c33d4a9dab98fbdfbcb4a5bd8bb2f5dce8b3d00c05f248d82c16a913d119",
            "probe-41292426657-1-e01-c1-action/probe-41292426657-1-e01-c1-action-q49vq.containers.probe.log": "0c1522b0b4db713dcef561c9612cb2a2396877c678b5d57a67a049d7aa4364ed",
            "probe-41292426657-1-e01-c1-judge/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-e01-c1-judge/pods.json": "c1dd50f05dbc2ce17767cb7f60a7e3b83468fd4a8789ec88346f6025432618a7",
            "probe-41292426657-1-e01-c1-judge/probe-41292426657-1-e01-c1-judge-dpf2b.containers.verifier.log": "0396a48f1c6cf4f8cc8decb9772236ba9ba11bf5219eafb3955fcb6171ecdfdd",
            "probe-41292426657-1-e01-c1-read/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-e01-c1-read/pods.json": "2b364119f0868192fde30e3e5064d84c7a8dd38ca677df52b4d66cb1f28cbcd1",
            "probe-41292426657-1-e01-c1-read/probe-41292426657-1-e01-c1-read-lbnxg.containers.probe.log": "5dd8941495219eef2b01744776997ba85e6e0c514c0a790a4ba50f3894b7b741",
            "probe-41292426657-1-e01-c1-seed/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-e01-c1-seed/pods.json": "ff14832a0c76da26343ad8b953965cfc6d88dcc64617b167e99e9292c6e6288b",
            "probe-41292426657-1-e01-c1-seed/probe-41292426657-1-e01-c1-seed-lp7pr.containers.enrichment-consumer.log": "39e6965c03aeffabbd42d60d149b0a2b290b22ee132d41dbe8a96d4fe50ccbcf",
            "probe-41292426657-1-e01-c1-seedread/collection-status.json": "9bb1833b9f09339b7b388c979969793b665bde391eb2c58b9f6572f3e04b8ca1",
            "probe-41292426657-1-e01-c1-seedread/pods.json": "c36a6f2b51f64b96c2061b7792b54ef2eec95584c5099fb5d24448fa20a566ef",
            "probe-41292426657-1-e01-c1-seedread/probe-41292426657-1-e01-c1-seedread-vpb6b.containers.probe.log": "bb7c3be6756998461e38c4ae853961131e0b96802796fdea0c23597ebb56031e"
        }
    },
    "phase-status.json": "90bc97993bf439904ffc58782e2f05f0774c3c26b6014c63eabe392e890ea7b1",
    "c5_summary": "3f1ba194816bab19b31b900c9084839d2f8250359b115bfe2df0304f505c6bec",
    "sources": {
        "scenarios/B08/scenario.yaml": "e4869d401bd366e8922043baa3bfa0a53a1fe1d406ee40f2077405bdc9b7bef4",
        "scenarios/B08/expected.yaml": "45bab8e908bf59a0ac83aae427adc426e1ce63649dfb619906b858f66f50365c",
        "scenarios/E01/scenario.yaml": "75d3810adbafd61b7cda676842cef70f86532c88e88516a78c1bcb2854533c07",
        "scenarios/E01/expected.yaml": "bd154eee17f69f120402a9415fbc30d2a3a0e06206115a0b6c5a78e5b9896a40",
        "infra/cluster/aks.tf": "969762e923eae813525207d50f5689de9fbed20c0c4c2c274367bcb54462a8a2"
    }
}

# Read from the batch job log of run 41292426657 (plan and apply of the ephemeral
# layer, then its destroy); the log itself is not a pinned input.
ENVIRONMENT = {
    "region": "uksouth",
    "event_hubs_sku": "Premium",
    "pu_count": 1,
    "partition_count_planned": {"document-events": 32, "document-state": 32},
    "node_size": "Standard_D4ds_v5",
    "node_size_evidence": "inferred_from_pinned_configuration_and_no_change_cluster_plan",
    "sources": [
        BATCH_JOB,
        REPO + "/blob/" + SOURCE + "/infra/cluster/aks.tf",
        REPO + "/blob/" + SOURCE + "/infra/ephemeral/eventhubs.tf",
    ],
}

EXPECTED = {
    "B08": {
        "configurations": ("C0", "C1"),
        "account": {"action": "loadgen", "read": "probe"},
        "stages": ("action", "read"),
        "partitioner": {"C0": "consistent_random", "C1": "murmur2_random"},
        "partition": {"C0": 30, "C1": 2},
        "topic": "document-events",
        "hypothesis": "H_partition",
    },
    "E01": {
        "configurations": ("C0", "C1"),
        "account": {"seed": "enrichment-consumer", "seedread": "probe", "action": "probe", "read": "probe"},
        "stages": ("seed", "seedread", "action", "read"),
        "value_kind": {"C0": "empty", "C1": "null"},
        "topic": "document-state",
        "hypothesis": "H_transport",
    },
}

# The 11 non-NULL of the 12 vectors of rd_murmur2() in librdkafka v2.16.0 src/rdmurmur2.c
# unittest_murmur2. The 12th is NULL and has the same expected value as the empty key.
MURMUR_VECTORS = (
    (b"kafka", 0xD067CF64), (b"giberish123456789", 0x8F552B0C), (b"1234", 0x9FC97B14),
    (b"234", 0xE7C009CA), (b"34", 0x873930DA), (b"4", 0x5A4B5CA1),
    (b"PreAmbleWillBeRemoved,ThePrePartThatIs", 0x78424F1C),
    (b"reAmbleWillBeRemoved,ThePrePartThatIs", 0x4A62B377),
    (b"eAmbleWillBeRemoved,ThePrePartThatIs", 0xE0E4E09E),
    (b"AmbleWillBeRemoved,ThePrePartThatIs", 0x62B8B43F), (b"", 0x106E08D9),
)
M32 = 0xFFFFFFFF


def fail(message):
    raise ValueError(message)


def require(condition, message):
    if not condition:
        fail(message)


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def pinned_bytes(path, digest):
    require(path.is_file(), "missing input " + str(path))
    data = path.read_bytes()
    require(sha256(data) == digest, str(path) + " does not match its pinned SHA-256")
    return data


def git(*args):
    return subprocess.check_output(("git", "-C", str(ROOT)) + args, stderr=subprocess.STDOUT)


def murmur2(data):
    """Port of rd_murmur2() in librdkafka v2.16.0 src/rdmurmur2.c, little-endian words."""
    m = 0x5BD1E995
    h = (0x9747B28C ^ len(data)) & M32
    i = 0
    while len(data) - i >= 4:
        k = int.from_bytes(data[i:i + 4], "little")
        k = (k * m) & M32
        k ^= k >> 24
        k = (k * m) & M32
        h = ((h * m) & M32) ^ k
        i += 4
    rest = len(data) - i
    if rest == 3:
        h ^= data[i + 2] << 16
    if rest >= 2:
        h ^= data[i + 1] << 8
    if rest >= 1:
        h ^= data[i]
        h = (h * m) & M32
    h ^= h >> 13
    h = (h * m) & M32
    h ^= h >> 15
    return h


def expected_partitions(key, partition_count):
    """consistent_random: rd_crc32(key) % n; murmur2_random: (rd_murmur2(key) & 0x7fffffff) % n.

    Both apply to a non-empty key (rdkafka_msg.c, v2.16.0). rd_crc32 is the
    standard reflected CRC-32 (poly 0x04c11db7, init and xorout 0xffffffff), which
    is zlib's crc32.
    """
    crc = zlib.crc32(key) & M32
    mur = murmur2(key)
    return {
        "crc32": crc,
        "murmur2": mur,
        "consistent_random": crc % partition_count,
        "murmur2_random": (mur & 0x7FFFFFFF) % partition_count,
    }


def load_json(path, digest):
    return json.loads(pinned_bytes(path, digest).decode("utf-8"))


def capture_line(path, digest):
    text = pinned_bytes(path, digest).decode("utf-8")
    lines = [l for l in text.splitlines() if l.startswith("PARCHWELL_PROBE_V2_CAPTURE ")]
    require(len(lines) == 1, str(path) + " does not hold exactly one capture line")
    return json.loads(lines[0][len("PARCHWELL_PROBE_V2_CAPTURE "):])


def pod_info(pods_dir, item, job, role):
    prefix = job + "/"
    members = {k: v for k, v in PINS[item]["pods"].items() if k.startswith(prefix)}
    require(len(members) == 3, job + " does not have exactly three pinned members")
    status = load_json(pods_dir / (prefix + "collection-status.json"), members[prefix + "collection-status.json"])
    require(status == {"complete": True, "gaps": []}, job + " pod collection is incomplete")
    doc = load_json(pods_dir / (prefix + "pods.json"), members[prefix + "pods.json"])
    require(len(doc["items"]) == 1, job + " does not have exactly one pod")
    pod = doc["items"][0]
    statuses = pod["status"]["containerStatuses"]
    require(len(statuses) == 1 and statuses[0]["name"] == role, job + " container differs from " + role)
    term = statuses[0]["state"]["terminated"]
    require(term["exitCode"] == 0 and term["reason"] == "Completed" and statuses[0]["restartCount"] == 0,
            job + " pod did not complete once with exit 0")
    log_names = [k for k in members if k.endswith(".log")]
    require(len(log_names) == 1, job + " does not have exactly one pinned log")
    name = pod["metadata"]["name"]
    require(log_names[0] == prefix + name + ".containers." + role + ".log", job + " log name differs from its pod")
    return {
        "job": job,
        "pod": name,
        "pod_uid": pod["metadata"]["uid"],
        "service_account": pod["spec"]["serviceAccountName"],
        "container": role,
        "container_started_at": term["startedAt"],
        "container_finished_at": term["finishedAt"],
        "exit_code": term["exitCode"],
        "image_digest": statuses[0]["imageID"].split("@", 1)[1],
        "node_pool": pod["spec"]["nodeName"].split("-")[1],
        "pod_log_member": log_names[0],
        "pod_json_member": prefix + "pods.json",
        "_log": pods_dir / log_names[0],
        "_log_hash": members[log_names[0]],
    }


def derive(item, candidates):
    """Closed rules transcribed from scenarios/<item>/expected.yaml (see PINS['sources'])."""
    if item == "B08":
        want = EXPECTED["B08"]["partition"]
        for config in ("C0", "C1"):
            cand = candidates[config]
            if cand["outcome_class"]["result"] != "success":
                return "untestable", "undecided", "action_error"
            for repeat in cand["repeats"]:
                so = repeat["semantic_outcome"]
                if so["value_match"] is not True:
                    fail("value mismatch would be incomplete, not a verdict")
                if so["delivered_partition"] != so["consumed_partition"]:
                    fail("delivered and consumed partitions differ")
        for config in ("C0", "C1"):
            for repeat in candidates[config]["repeats"]:
                if repeat["semantic_outcome"]["delivered_partition"] != want[config]:
                    return "contradicted", "contradicted", None
        return "confirmed", "holds", None
    kinds = {c: {r["semantic_outcome"]["value_kind"] for r in candidates[c]["repeats"]} for c in ("C0", "C1")}
    for config in ("C0", "C1"):
        require(candidates[config]["outcome_class"]["result"] == "success", "E01 error is not a hold")
    if kinds["C0"] != {"empty"}:
        return "untestable", "undecided", "failed_control"
    if kinds["C1"] == {"null"}:
        return "confirmed", "holds", None
    return "contradicted", "contradicted", None


def verify_candidates(item, cand_dir):
    spec = EXPECTED[item]
    candidates, receipts, raw = {}, {}, {}
    for config in spec["configurations"]:
        name = "%s-%s.json" % (item, config)
        data = pinned_bytes(cand_dir / name, PINS[item]["candidates"][name])
        cand = json.loads(data.decode("utf-8"))
        rname = "%s-%s-judge-completion.json" % (item, config)
        receipt = load_json(cand_dir / rname, PINS[item]["candidates"][rname])
        raw[config] = data
        for key, value in (("schema_version", 2), ("mode", "single-action-v2"), ("synthetic", True),
                           ("run_id", RUN_ID), ("scenario_id", item), ("configuration_id", config),
                           ("source_commit", SOURCE), ("managed_version", "2.16.0.0"),
                           ("native_version", "2.16.0"), ("status", "complete_valid"),
                           ("deterministic", True), ("adr012_counts", "not_applicable"),
                           ("diagnostics", ["full_experiment"]), ("security_finding", False)):
            require(cand[key] == value, "%s %s differs: %r" % (name, key, cand[key]))
        require(cand["outcome_class"] == {"result": "success", "error_code": None, "is_fatal": None, "origin": None},
                name + " outcome class differs")
        require(sorted(cand["image_digests"]) == ["loadgen", "verifier"], name + " image digest keys differ")
        require([r["repeat"] for r in cand["repeats"]] == [1, 2, 3, 4, 5], name + " repeat ordinals differ")
        for repeat in cand["repeats"]:
            require(repeat["status"] == "complete_valid" and repeat["reasons"] == [], name + " repeat is not complete_valid")
            require(repeat["outcome_class"]["result"] == "success", name + " repeat is not a success")
        require(receipt["candidate_sha256"] == sha256(data), rname + " does not bind the candidate bytes")
        for key, value in (("schema_version", 2), ("mode", "single-action-v2-judge"), ("run_id", RUN_ID),
                           ("scenario_id", item), ("configuration_id", config), ("source_commit", SOURCE),
                           ("handler_exit_code", 0), ("cleanup_completed", True)):
            require(receipt[key] == value, "%s %s differs" % (rname, key))
        require(receipt["candidate_name"] == "%s/single-action-v2/%s/%s/candidate.json" % (RUN_ID, item, config),
                rname + " candidate name differs")
        candidates[config], receipts[config] = cand, receipt
    require(candidates["C0"]["item_assessment"] is None, "nonfinal candidate carries an assessment")
    require(candidates["C0"]["image_digests"] == candidates["C1"]["image_digests"], "image digests differ")
    return candidates, receipts


def verify_pods(item, pods_dir, candidates, receipts):
    spec = EXPECTED[item]
    status = load_json(pods_dir / "phase-status.json", PINS["phase-status.json"])
    require(status["run_id"] == RUN_ID and status["phase"] == "single-action", "phase status identity differs")
    require(status["safety_stop"] is False and status["not_selected"] == [], "phase status safety or selection differs")
    modes = [m for m in status["modes"] if m["item"] == item]
    jobs = {}
    for config in spec["configurations"]:
        for stage in spec["stages"] + ("judge",):
            rows = [m for m in modes if m["configuration"] == config and m["stage"] == stage]
            require(len(rows) == 1 and rows[0]["outcome"] == "complete", "%s %s %s is not complete" % (item, config, stage))
            if stage != "judge":
                require(rows[0]["capture_complete"] is True and rows[0]["admitted_client_slots"] == 5,
                        "%s %s %s capture is not complete with five slots" % (item, config, stage))
            role = "verifier" if stage == "judge" else spec["account"][stage]
            job = "%s-%s-%s-%s" % (RUN_ID, item.lower(), config.lower(), stage)
            require(rows[0]["job"] == job, "phase status Job name differs")
            info = pod_info(pods_dir, item, job, role)
            digest = candidates[config]["image_digests"]["verifier" if stage == "judge" else "loadgen"]
            require(info["image_digest"] == digest, job + " ran another image digest")
            require(info["service_account"] == role, job + " ran as another service account")
            jobs[(config, stage)] = info
        judge = jobs[(config, "judge")]
        require(receipts[config]["pod_name"] == judge["pod"] and receipts[config]["pod_uid"] == judge["pod_uid"]
                and receipts[config]["job_name"] == judge["job"], "judge receipt does not bind its pod")
    entries = [c for c in status["candidates"] if c["item"] == item]
    require(len(entries) == 2 and all(c["accepted"] is True and c["evidence_valid"] is True and c["smoke_valid"] is False
                                      for c in entries), "phase status does not accept both candidates")
    return jobs, status


def verify_captures(item, jobs, candidates):
    spec = EXPECTED[item]
    captures = {}
    for (config, stage), info in sorted(jobs.items()):
        if stage == "judge":
            continue
        cap = capture_line(info["_log"], info["_log_hash"])
        for key, value in (("run_id", RUN_ID), ("scenario_id", item), ("configuration_id", config), ("stage", stage),
                           ("source_commit", SOURCE), ("job_name", info["job"]), ("pod_name", info["pod"]),
                           ("pod_uid", info["pod_uid"]), ("service_account", info["service_account"]),
                           ("handler_exit_code", 0), ("cleanup_completed", True), ("host_cleanup_completed", True)):
            require(cap[key] == value, "%s %s %s capture %s differs" % (item, config, stage, key))
        require([r["repeat"] for r in cap["repeats"]] == [1, 2, 3, 4, 5], "capture ordinals differ")
        for r in cap["repeats"]:
            require(r["client_id"] == "%s-r%d" % (info["pod"], r["repeat"]) and r["admission"] == "required"
                    and r["terminal"] is True, "capture client binding differs")
        captures[(config, stage)] = cap
    for config in spec["configurations"]:
        cand = candidates[config]
        action, read = captures[(config, "action")], captures[(config, "read")]
        for i, repeat in enumerate(cand["repeats"]):
            a, r = action["repeats"][i], read["repeats"][i]
            require(a["delivery"]["persistence_status"] == "Persisted" and a["delivery"]["error"] is None
                    and a["result"] == "success" and r["result"] == "success", "capture result differs")
            require(repeat["delivery"] == a["delivery"], "candidate delivery differs from the action capture")
            require(repeat["read_observation"]["record"] == r["read_observation"]["record"],
                    "candidate read record differs from the read capture")
            rec = r["read_observation"]["record"]
            d = a["delivery"]
            require((rec["topic"], rec["partition"], rec["offset"]) == (d["topic"], d["partition"], d["offset"])
                    and rec["key_base64"] == a["requested"]["key_base64"], "read record is not the delivered location and key")
            require(d["topic"] == spec["topic"] and a["requested"]["topic"] == spec["topic"], "topic differs")
            if item == "B08":
                require(a["settings"]["partitioner"] == spec["partitioner"][config], "producer partitioner differs")
                require(a["requested"]["partition_mode"] == "automatic" and a["requested"]["partition"] is None,
                        "B08 producer did not request automatic partitioning")
                require(rec["value_base64"] == a["requested"]["value_base64"], "B08 read value differs from the sent value")
                for cap_r in (a, r):
                    require(sorted(cap_r["metadata_partitions"]) == list(range(32)), "metadata partitions are not 0 to 31")
            else:
                require(a["requested"]["partition_mode"] == "explicit" and a["requested"]["partition"] == 0
                        and d["partition"] == 0, "E01 target did not use explicit partition 0")
                want = {"empty": "", "null": None}[spec["value_kind"][config]]
                require(a["requested"]["value_base64"] == want and rec["value_base64"] == want, "E01 target value differs")
                seed, seedread = captures[(config, "seed")]["repeats"][i], captures[(config, "seedread")]["repeats"][i]
                sd, sr = seed["delivery"], seedread["read_observation"]["record"]
                require(sd["persistence_status"] == "Persisted" and (sr["partition"], sr["offset"]) == (sd["partition"], sd["offset"])
                        and sr["value_base64"] == seed["requested"]["value_base64"] and sr["value_base64"] not in (None, ""),
                        "E01 seed was not read back non-null at its delivered location")
                require(sr["partition"] == d["partition"] == 0 and sr["key_base64"] == rec["key_base64"]
                        and sd["offset"] < d["offset"], "E01 seed and target are not on one key and partition with a lower seed offset")
    return captures


def verify_c5(c5_path):
    summary = load_json(c5_path, PINS["c5_summary"])
    require(summary["commit"] == C5_COMMIT and summary["run_id"] == "41232286130"
            and summary["run_attempt"] == "1", "C5 summary identity differs")
    require(summary["measurement_complete"] is True and summary["incomplete_reasons"] == [] and summary["break_passed"] is True,
            "C5 summary is not complete and passing")
    targets = summary["targets"]
    require(sum(t["numerator"] for t in targets.values()) == summary["numerator"]
            and sum(t["denominator"] for t in targets.values()) == summary["denominator"], "C5 totals do not add up")
    config = json.loads(git("show", C5_COMMIT + ":" + C5_CONFIG))["stryker-config"]
    require(config["thresholds"]["break"] == 60, "C5 break threshold differs")
    mutate = config["mutate"]
    require(sorted(t["target"] for t in targets.values()) == sorted(mutate), "C5 targets differ from the configuration")
    same = {}
    for target in mutate:
        path = "src/Parchwell.Verifier/" + target
        same[target] = git("rev-parse", C5_COMMIT + ":" + path).strip() == git("rev-parse", SOURCE + ":" + path).strip()
    config_same = git("show", C5_COMMIT + ":" + C5_CONFIG) == git("show", SOURCE + ":" + C5_CONFIG)
    require(subprocess.call(("git", "-C", str(ROOT), "merge-base", "--is-ancestor", C5_COMMIT, SOURCE)) == 0,
            "the C5 commit is not an ancestor of the run source")
    return {
        "run": C5_RUN,
        "summary_artifact": C5_SUMMARY_ARTIFACT,
        "commit": summary["commit"],
        "measurement_complete": True,
        "break_passed": True,
        "break_threshold": config["thresholds"]["break"],
        "numerator": summary["numerator"],
        "denominator": summary["denominator"],
        "score_percent_rounded_1dp": round(summary["numerator"] / summary["denominator"] * 100, 1),
        "status_counts": summary["status_counts"],
        "targets": {t["target"]: {"status_counts": t["status_counts"], "numerator": t["numerator"],
                                  "denominator": t["denominator"]} for t in targets.values()},
        "target_source_identical_at_run_source": same,
        "all_targets_identical_at_run_source": all(same.values()),
        "config_identical_at_run_source": config_same,
        "c5_commit_is_ancestor_of_run_source": True,
        "run_source": SOURCE,
    }


def build(item, cand_dir, pods_dir, c5_path):
    spec = EXPECTED[item]
    # Pin the checked-out inputs that define the item before using anything.
    for path, digest in PINS["sources"].items():
        require(sha256(git("show", SOURCE + ":" + path)) == digest, path + " at the run source differs from its pin")
        if path.startswith("scenarios/%s/" % item):
            pinned_bytes(ROOT / path, digest)
    for vec, want in MURMUR_VECTORS:
        require(murmur2(vec) == want, "murmur2 port differs from the pinned source vector")
    candidates, receipts = verify_candidates(item, cand_dir)
    jobs, status = verify_pods(item, pods_dir, candidates, receipts)
    captures = verify_captures(item, jobs, candidates)
    verdict, hypothesis, reason = derive(item, candidates)
    final = candidates["C1"]["item_assessment"]
    require(final == {"verdict": verdict, "hypotheses": {spec["hypothesis"]: hypothesis}, "reason": reason},
            "derived assessment differs from the verifier's")
    require(sorted(final["hypotheses"]) == [spec["hypothesis"]], "hypothesis label differs")
    c5 = verify_c5(c5_path)

    per_conf = {}
    for config in spec["configurations"]:
        cand = candidates[config]
        clients = []
        for repeat in cand["repeats"]:
            for client in repeat["clients"]:
                clients.append({"repeat": repeat["repeat"], "stage": client["stage"], "client_id": client["client_id"],
                                "broker_count": client["broker_count"]})
        per_conf[config] = {
            "status": cand["status"], "deterministic": cand["deterministic"], "outcome_class": cand["outcome_class"],
            "broker_count_per_repeat": [r["broker_count"] for r in cand["repeats"]],
            "broker_count_lower_bound": True,
            "broker_count_per_client": clients,
        }
    observed = {}
    for config in spec["configurations"]:
        rows = []
        for repeat in candidates[config]["repeats"]:
            so, d = repeat["semantic_outcome"], repeat["delivery"]
            row = {"repeat": repeat["repeat"], "delivered_partition": so["delivered_partition"],
                   "consumed_partition": so["consumed_partition"], "offset": d["offset"],
                   "key": base64.b64decode(so["key_base64"]).decode("ascii"), "value_kind": so["value_kind"],
                   "value_match": so["value_match"]}
            if item == "E01":
                a = captures[(config, "action")]["repeats"][repeat["repeat"] - 1]
                s = captures[(config, "seed")]["repeats"][repeat["repeat"] - 1]
                row["seed_offset"] = s["delivery"]["offset"]
                row["seed_value"] = base64.b64decode(s["requested"]["value_base64"]).decode("ascii")
                row["target_value_base64"] = a["requested"]["value_base64"]
            rows.append(row)
        observed[config] = rows
    extra = {}
    if item == "B08":
        key = base64.b64decode(candidates["C0"]["repeats"][0]["semantic_outcome"]["key_base64"])
        require(all(base64.b64decode(r["semantic_outcome"]["key_base64"]) == key
                    for c in candidates.values() for r in c["repeats"]), "B08 keys differ")
        math = expected_partitions(key, 32)
        for config in spec["configurations"]:
            require(math[spec["partitioner"][config]] == spec["partition"][config], "computed partition differs from the plan")
            require(all(r["delivered_partition"] == math[spec["partitioner"][config]] for r in observed[config]),
                    "delivered partition differs from the independent computation")
        extra["independent_computation"] = {
            "key": key.decode("ascii"), "partition_count": 32, "crc32": math["crc32"], "murmur2": math["murmur2"],
            "expected_partition": {"C0": math["consistent_random"], "C1": math["murmur2_random"]},
            "basis": "rdkafka_msg.c, rdmurmur2.c and rdcrc32.h at librdkafka v2.16.0; a computed expectation, not a measurement",
            "murmur2_port_checked_against_non_null_source_vectors": len(MURMUR_VECTORS),
        }
        extra["measured_partitioner"] = {c: spec["partitioner"][c] for c in spec["configurations"]}

    job_rows = {}
    for (config, stage), info in sorted(jobs.items()):
        job_rows.setdefault(config, {})[stage] = {k: v for k, v in info.items() if not k.startswith("_")}
    summary = {
        "B08": [
            "Across 10 complete synthetic repeats in two deterministic configurations, the default consistent_random partitioner delivered the fixed key to partition 30 in all five repeats and murmur2_random delivered it to partition 2 in all five, each equal to the partition computed from the pinned librdkafka v2.16.0 algorithms for a 32-partition event hub, so H_partition holds and the proposed B08 verdict is confirmed.",
            "An independent consumer read back the same topic, partition, offset, key and value in every repeat, and each producer and reader reported partition IDs exactly 0 to 31 in its own metadata.",
            "One key, one event hub of 32 partitions and one producer message per repeat were tested; behaviour for other keys, other partition counts, keyless messages and the sticky and random partitioners was not.",
        ],
        "E01": [
            "Across 10 complete synthetic repeats in two deterministic configurations, a produce with an empty value read back as an empty value in all five repeats of C0 and a produce with a genuine null value read back as a null value in all five repeats of C1, each after an independently read non-null seed on the same key and partition, so H_transport holds and the proposed E01 verdict is confirmed.",
            "The pinned Confluent.Kafka byte-array serializer and deserializer pass null and empty through as distinct values, so the result shows that Event Hubs preserves that distinction on the Kafka produce and consume path for this one partition.",
            "Only transport was tested; compaction timing, tombstone retention, deletion of the seed and recovery after compaction were not, even though the topic is configured with the Compact cleanup policy.",
        ],
    }[item]
    publication = {
        "item": item,
        "proposed_verdict": verdict,
        "summary": summary,
        "hypotheses": final["hypotheses"],
        "derivation": "decision rules transcribed from scenarios/%s/expected.yaml (SHA-256 %s) into make_results.py; derived verdict equal to the verifier item_assessment of C1 (C0 carries null, as the rules require)" % (item, PINS["sources"]["scenarios/%s/expected.yaml" % item]),
        "per_configuration": per_conf,
        "observed_per_repeat": observed,
        "error_code_names": {},
        "adr012_counts": "not_applicable",
        "run": {
            "workflow_run": RUN,
            "attempt": 1,
            "run_id": RUN_ID,
            "source_commit": SOURCE,
            "run_conclusion": "failure",
            "run_conclusion_scope": "F06 permission audit defect, fixed in pull request 288; Hari decided it does not block B08 or E01",
            "candidate_artifact": CANDIDATE_ARTIFACT,
            "pod_artifact": POD_ARTIFACT,
            "batch_job": BATCH_JOB,
            "batch_stop_job": STOP_JOB,
            "jobs": job_rows,
        },
        "versions": {"confluent_kafka_managed": "2.16.0.0", "librdkafka_native": "2.16.0",
                     "image_digests": candidates["C0"]["image_digests"]},
        "environment": ENVIRONMENT,
        "mutation_gate": c5,
        "workload": "synthetic",
    }
    publication.update(extra)
    result = {"publication": publication,
              "candidates": candidates,
              "judge_receipts": receipts}
    return result, jobs, candidates


def render_tables(item, result, jobs, candidates):
    pub = result["publication"]
    out = []
    out.append("<!-- tables:repeat-outcomes -->")
    if item == "B08":
        out.append("| Configuration | Partitioner | Repeat | Status | Result | Delivered partition | Delivered offset | Read partition | Key | Value matches | Producer brokers |")
        out.append("| --- | --- | ---: | --- | --- | ---: | ---: | ---: | --- | --- | ---: |")
    else:
        out.append("| Configuration | Target value | Repeat | Status | Result | Seed offset | Target offset | Partition | Read-back value | Value matches | Producer brokers |")
        out.append("| --- | --- | ---: | --- | --- | ---: | ---: | ---: | --- | --- | ---: |")
    for config in ("C0", "C1"):
        for repeat, row in zip(candidates[config]["repeats"], pub["observed_per_repeat"][config]):
            if item == "B08":
                out.append("| %s | `%s` | %d | `%s` | `%s` | %d | %d | %d | `%s` | %s | %d |" % (
                    config, EXPECTED[item]["partitioner"][config], repeat["repeat"], repeat["status"],
                    repeat["outcome_class"]["result"], row["delivered_partition"], row["offset"],
                    row["consumed_partition"], row["key"], "yes" if row["value_match"] else "no", repeat["broker_count"]))
            else:
                out.append("| %s | `%s` | %d | `%s` | `%s` | %d | %d | %d | `%s` | %s | %d |" % (
                    config, "empty" if config == "C0" else "null", repeat["repeat"], repeat["status"],
                    repeat["outcome_class"]["result"], row["seed_offset"], row["offset"], row["delivered_partition"],
                    row["value_kind"], "yes" if row["value_match"] else "no", repeat["broker_count"]))
    out.append("<!-- /tables:repeat-outcomes -->")
    out.append("")
    out.append("<!-- tables:client-broker-counts -->")
    stages = EXPECTED[item]["stages"]
    out.append("| Configuration | Repeat | " + " | ".join("%s brokers" % s for s in stages) + " |")
    out.append("| --- | ---: | " + " | ".join("---:" for _ in stages) + " |")
    for config in ("C0", "C1"):
        counts = {}
        for client in pub["per_configuration"][config]["broker_count_per_client"]:
            counts[(client["repeat"], client["stage"])] = client["broker_count"]
        for n in range(1, 6):
            out.append("| %s | %d | " % (config, n) + " | ".join(str(counts[(n, s)]) for s in stages) + " |")
    out.append("<!-- /tables:client-broker-counts -->")
    out.append("")
    out.append("<!-- tables:jobs -->")
    out.append("| Configuration | Stage | Job (account) | Container started | Container finished | Pod log member | Pod JSON member |")
    out.append("| --- | --- | --- | --- | --- | --- | --- |")
    for config in ("C0", "C1"):
        for stage in EXPECTED[item]["stages"] + ("judge",):
            j = {k: v for k, v in jobs[(config, stage)].items() if not k.startswith("_")}
            out.append("| %s | %s | `%s` (`%s`) | %s | %s | `%s` | `%s` |" % (
                config, stage, j["job"], j["service_account"], j["container_started_at"], j["container_finished_at"],
                j["pod_log_member"], j["pod_json_member"]))
    out.append("<!-- /tables:jobs -->")
    out.append("")
    out.append("<!-- tables:mutation-targets -->")
    out.append("| Target | Killed | Timeout | Survived | NoCoverage | CompileError | RuntimeError | Ignored | Detected / scored | Identical at run source |")
    out.append("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |")
    c5 = pub["mutation_gate"]
    for target, t in c5["targets"].items():
        s = t["status_counts"]
        out.append("| `%s` | %d | %d | %d | %d | %d | %d | %d | %d / %d | %s |" % (
            target, s["Killed"], s["Timeout"], s["Survived"], s["NoCoverage"], s["CompileError"], s["RuntimeError"],
            s["Ignored"], t["numerator"], t["denominator"], "yes" if c5["target_source_identical_at_run_source"][target] else "no"))
    s = c5["status_counts"]
    out.append("| All six targets | %d | %d | %d | %d | %d | %d | %d | %d / %d | %s |" % (
        s["Killed"], s["Timeout"], s["Survived"], s["NoCoverage"], s["CompileError"], s["RuntimeError"], s["Ignored"],
        c5["numerator"], c5["denominator"], "yes" if c5["all_targets_identical_at_run_source"] else "no"))
    out.append("<!-- /tables:mutation-targets -->")
    return "\n".join(out) + "\n"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--item", required=True, choices=sorted(EXPECTED))
    parser.add_argument("--candidates", required=True, type=pathlib.Path)
    parser.add_argument("--pods", required=True, type=pathlib.Path)
    parser.add_argument("--c5-summary", required=True, type=pathlib.Path)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--tables", action="store_true")
    args = parser.parse_args()
    result, jobs, candidates = build(args.item, args.candidates, args.pods, args.c5_summary)
    data = (json.dumps(result, indent=2, ensure_ascii=True) + "\n").encode("ascii")
    target = ROOT / "results" / args.item / (DATE + ".json")
    report = ROOT / "results" / args.item / (DATE + ".md")
    tables = render_tables(args.item, result, jobs, candidates)
    if args.tables:
        sys.stdout.write(tables)
        return
    if args.check:
        require(target.is_file() and target.read_bytes() == data, "published result bytes differ")
        text = report.read_text(encoding="utf-8")
        for block in tables.strip().split("\n\n"):
            require(block in text, "report does not contain the generated table " + block.splitlines()[0])
        print("check passed")
        return
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)
    print("wrote", target.relative_to(ROOT))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, subprocess.CalledProcessError) as error:
        print("make_results: " + str(error), file=sys.stderr)
        sys.exit(1)

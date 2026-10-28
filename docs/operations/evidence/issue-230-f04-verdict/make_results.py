"""Assemble the F04 publication from final run 40722187672, without network access.

Supported inputs are the extracted candidate and public pod artifacts of attempt
1, the C5 summary from run 40642331044, and this checkout's pinned F04 scenario,
expected outcome and cluster configuration. Run from any working directory:

    python3 -I docs/operations/evidence/issue-230-f04-verdict/make_results.py \
        --candidates DIR --pods DIR --c5-summary FILE [--check]

Only results/F04/2026-10-28.json is written; --check compares its exact bytes.
This is the issue-210 publication assembler precedent, not a second scientific
validator. The verifier's complete-valid projections remain authoritative. All
five candidates and judge receipts are preserved unchanged, re-serialized.
The closed F04 decision rules below are transcribed from the pinned expected.yaml.

Failure conditions: a pinned input hash differs or an input is missing; candidate
identity/completion/repeat projections or derived assessment differ; a receipt
fails its actual pod name/UID binding; pod collection is incomplete or has gaps,
other than one successful, unrestarted expected-account Job pod, or a different
image digest; the seed prerequisite receipt differs; C2's five capped-log markers
differ; C5 completion or break projections differ; or --check output differs.
Only approved projections and publication provenance are read from pod JSON;
raw pod documents and debug lines are never copied into the publication.
Regression example: copy the candidate directory to a temporary directory,
append a newline to F04-C0.json, and use it with --check: the pinned SHA-256
check must fail before a result can be written. The original inputs stay intact.
"""

import argparse
import base64
import hashlib
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[4]
REPO = "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet"
RUN = REPO + "/actions/runs/40722187672"
RUN_ID = "probe-40722187672-1"
SOURCE = "20f17794330b60d77a75bf5788ee442cc8baa1f6"
CONFIGURATIONS = ("C0", "C1", "C2", "C3", "C4")
CANDIDATE_ARTIFACT = RUN + "/artifacts/12834018507"
POD_ARTIFACT = RUN + "/artifacts/12835061323"
C5_RUN = REPO + "/actions/runs/40642331044"
C5_SOURCE = "1f14866faf395ecb28d3b9009986c1b332074386"
TARGET = ROOT / "results/F04/2026-10-28.json"

# Exact public artifact members; unrelated C08 evidence is outside this pack.
CANDIDATE_HASHES = {
    "F04-C0-judge-completion.json": "234f804af38a8fe46f8c305f56d93f7e8f797de2c546ce85f86be41bb6ca6e84",
    "F04-C0.json": "d5e052dd69bc9bdfdca4684af1caeda4f3395070b2e294885fbc545b2014b21a",
    "F04-C1-judge-completion.json": "13707129569abbc0457076a66dc35c7d5abda583a761d81bcf7ef0b455e338ad",
    "F04-C1.json": "4995ea7c48b8420faa90c8d3e28acc51e20bf91e2f6cfd34a02026f552d9e36b",
    "F04-C2-judge-completion.json": "d0ae1fc5c52c1a925fad35d0e4ecf36f18a60966d42720e7516928d8472bace4",
    "F04-C2.json": "cb60c2edfa74dcca380746d32a275b2d2935d4183da44083c3dbcab5214c8619",
    "F04-C3-judge-completion.json": "b7485bafe64436de12df4e452072de03eeac7fca3e8c6da8ec6a1df901bb3c58",
    "F04-C3.json": "1441bb3fe736098eb14c1a40aeb9c2c4366e11ca091dedfa9b78bdda3b73c113",
    "F04-C4-judge-completion.json": "337706343544a524ad20603be1df276d84325ea67920bfe61c9aeb2a66478992",
    "F04-C4.json": "379c17b67160bf9fa71a75cc29cc5bce142c687aedf2274a1eaa6bdbb049462b"
}
POD_HASHES = {
    "probe-40722187672-1-f04-c0-single/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "probe-40722187672-1-f04-c0-single/pods.json": "4280b1e5d0e5b72ff64fa906e11be2ae736c4447bae5f8e98328a8809db68c5c",
    "probe-40722187672-1-f04-c0-single/probe-40722187672-1-f04-c0-single-7fgxl.containers.loadgen.log": "df8bc83a7ce9f53821cc225aedb7f6311a3197d1d7c8f427e24119065ef93648",
    "probe-40722187672-1-f04-c0-single-judge/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "probe-40722187672-1-f04-c0-single-judge/pods.json": "489f1e07356471db128ea9bbe663569dc570e7fa49d9ebbe94fdd5f4b477ad5c",
    "probe-40722187672-1-f04-c0-single-judge/probe-40722187672-1-f04-c0-single-judge-wchjp.containers.verifier.log": "dfe9074af4188ff1af46809ce1853d2bb92ca2d712d9929b84d985a12f8ab76c",
    "probe-40722187672-1-f04-c1-single/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "probe-40722187672-1-f04-c1-single/pods.json": "a207e9d0c7a2ab420cf659151206d7faf496e8f78b8750e3c1b6f472aa1a4b56",
    "probe-40722187672-1-f04-c1-single/probe-40722187672-1-f04-c1-single-zngf5.containers.loadgen.log": "2ab99c9460db9768d2e8def6199fc8cea0550e9acad639706158624dc2b99066",
    "probe-40722187672-1-f04-c1-single-judge/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "probe-40722187672-1-f04-c1-single-judge/pods.json": "4065c524c537fe0e1b5096c96ada5a0f1dd8bd8cd583c6bc06be7f6472bc1955",
    "probe-40722187672-1-f04-c1-single-judge/probe-40722187672-1-f04-c1-single-judge-2ck4q.containers.verifier.log": "1e162e4a5f34da5a3f0354d60b9e1067a23f208923c644b7035a9143bde179a1",
    "probe-40722187672-1-f04-c2-single/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "probe-40722187672-1-f04-c2-single/pods.json": "2e57a8b60b3bbd68e5371dd360625f9152007b7f4eba027b7716954aabe2be21",
    "probe-40722187672-1-f04-c2-single/probe-40722187672-1-f04-c2-single-hgdmk.containers.probe.log": "de910d9bd9279726e17d3193e3edeed7fcbccab4ec716d9496008410d71a6631",
    "probe-40722187672-1-f04-c2-single-judge/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "probe-40722187672-1-f04-c2-single-judge/pods.json": "3b136563fc135c21d7e763b6f7a72e2d76e2a3a0317ed70da743c5979c23b22e",
    "probe-40722187672-1-f04-c2-single-judge/probe-40722187672-1-f04-c2-single-judge-r2gks.containers.verifier.log": "acbc3c3d23fc33da8b5b3ad8aa79c2339e44600c5b61a69bae573814264b6e25",
    "probe-40722187672-1-f04-c3-single/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "probe-40722187672-1-f04-c3-single/pods.json": "37ebc5b2432c9a9829769d6fc9571c18fd35414c68ac39c3f54c284b48b66232",
    "probe-40722187672-1-f04-c3-single/probe-40722187672-1-f04-c3-single-cg9jl.containers.loadgen.log": "6107c5a4d1589fef572c864750ac57c3ceccae2ec7d740fa75a1658069a4015d",
    "probe-40722187672-1-f04-c3-single-judge/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "probe-40722187672-1-f04-c3-single-judge/pods.json": "feca2a8004ad65be411077f7d59b7449d6398e84f403806ae9ec2de68e6eec67",
    "probe-40722187672-1-f04-c3-single-judge/probe-40722187672-1-f04-c3-single-judge-kb6zk.containers.verifier.log": "afa2595c3eb6c769f283749e07ef9b9acf9fd9d299e2caa3715e409a318f2b25",
    "probe-40722187672-1-f04-c4-single/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "probe-40722187672-1-f04-c4-single/pods.json": "c188a3a4654321756122f9f583b9db497e18a03021cbefc6c9e652677859f4b4",
    "probe-40722187672-1-f04-c4-single/probe-40722187672-1-f04-c4-single-wckjw.containers.probe.log": "faa7e16cc53ee3289dfe2648cd6899e53fe2feaa56f5a85eb717ad100e9752e3",
    "probe-40722187672-1-f04-c4-single-judge/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "probe-40722187672-1-f04-c4-single-judge/pods.json": "def727bb2e5fe59be35211976a942dde9317dbd79ecddda2ad98388ded2d1e4a",
    "probe-40722187672-1-f04-c4-single-judge/probe-40722187672-1-f04-c4-single-judge-64hf9.containers.verifier.log": "6406773b3c7389b16fd8b7f76a56e761e744520aab6e4f3674a56fd54a31ec8b",
    "probe-40722187672-1-f04-seed/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "probe-40722187672-1-f04-seed/pods.json": "ed7378bca8f6c3c59eef07dd587ca744e5f7044e8c6730e57cc1499236364d9a",
    "probe-40722187672-1-f04-seed/probe-40722187672-1-f04-seed-8dr2d.containers.enrichment-consumer.log": "d8104fb3addb3f4447d863b96edd57f6ccaf67053037410f089afb69b33acabc"
}
SOURCE_HASHES = {
    "scenarios/F04/scenario.yaml": "11409c59fd070b08473522d80ccee73a0c76fc9d83e0d4d79d2e2104ac9ceedb",
    "scenarios/F04/expected.yaml": "1354fc4a4531b63c9e7707c6d360faca97b0788faeca08842b504415d6967d5a",
    "infra/cluster/aks.tf": "969762e923eae813525207d50f5689de9fbed20c0c4c2c274367bcb54462a8a2"
}
C5_HASH = "bfdca0521f24944a458fb125b09f761ac45cd71343933a23b0e1fddcebc532bb"


def fail(message):
    raise ValueError(message)


def require(condition, message):
    if not condition:
        fail(message)


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def pinned(path, digest):
    data = path.read_bytes()
    require(sha256(data) == digest, str(path) + " does not match its pinned SHA-256")
    return data


def matches(document, expected, label):
    for key, value in expected.items():
        require(document.get(key) == value, f"{label}: unexpected {key}")


def pod_evidence(pods, job, account, image_digest, configuration=None):
    directory = pods / job
    members = {p.name for p in directory.iterdir() if p.is_file()}
    expected_members = {pathlib.PurePosixPath(p).name for p in POD_HASHES
                        if pathlib.PurePosixPath(p).parent.name == job}
    require(members == expected_members, job + " pod member set differs")
    status = json.loads((directory / "collection-status.json").read_bytes())
    require(status == {"complete": True, "smoke_marker_valid": False, "gaps": []},
            job + " collection is not complete")
    items = json.loads((directory / "pods.json").read_bytes())["items"]
    require(len(items) == 1, job + " must have exactly one pod")
    pod = items[0]
    require(pod["spec"]["serviceAccountName"] == account and pod["status"]["phase"] == "Succeeded",
            job + " account or phase differs")
    owners = pod["metadata"]["ownerReferences"]
    require(len(owners) == 1 and owners[0]["kind"] == "Job"
            and owners[0]["name"] == job and owners[0]["controller"] is True,
            job + " owner differs")
    require(pod["metadata"]["labels"]["app.kubernetes.io/instance"] == RUN_ID,
            job + " run binding differs")
    containers = pod["status"]["containerStatuses"]
    specs = pod["spec"]["containers"]
    require(len(containers) == len(specs) == 1, job + " must have exactly one container")
    container = containers[0]
    terminated = container["state"]["terminated"]
    require(terminated["exitCode"] == 0 and container["restartCount"] == 0,
            job + " exit or restart differs")
    require(container["imageID"].endswith("@" + image_digest)
            and specs[0]["image"].endswith("@" + image_digest), job + " image digest differs")
    env = {entry["name"]: entry.get("value") for entry in specs[0]["env"]}
    matches(env, {"PARCHWELL_SOURCE_COMMIT": SOURCE, "PARCHWELL_JOB_NAME": job}, job)
    if configuration is not None:
        prefix = "Verifier__" if job.endswith("single-judge") else "SingleAction__"
        matches(env, {prefix + "RunId": RUN_ID, prefix + "ScenarioId": "F04",
                      prefix + "ConfigurationId": configuration}, job)
    else:
        matches(env, {"SeedF04__RunId": RUN_ID, "SeedF04__SourceCommit": SOURCE}, job)
    logs = [name for name in members if name.endswith(".log")]
    require(len(logs) == 1, job + " must have exactly one log")
    return {
        "job": job, "pod": pod["metadata"]["name"], "pod_uid": pod["metadata"]["uid"],
        "service_account": account,
        "container_started_at": terminated["startedAt"],
        "container_finished_at": terminated["finishedAt"],
        "exit_code": terminated["exitCode"], "restart_count": container["restartCount"],
        "image_digest": image_digest,
        "pod_log_member": job + "/" + logs[0], "pod_json_member": job + "/pods.json",
        "collection_status_member": job + "/collection-status.json",
    }


def check_candidate(configuration, candidate, receipt, judge):
    matches(candidate, {
        "schema_version": 1, "mode": "single-action", "synthetic": True,
        "run_id": RUN_ID, "scenario_id": "F04", "configuration_id": configuration,
        "source_commit": SOURCE, "status": "complete_valid", "deterministic": True,
        "adr012_counts": "not_applicable", "diagnostics": [], "security_finding": False,
        "managed_version": "2.16.0.0", "native_version": "2.16.0",
    }, configuration)
    require([r["repeat"] for r in candidate["repeats"]] == list(range(1, 6)),
            configuration + " repeat identities differ")
    for repeat in candidate["repeats"]:
        matches(repeat, {"status": "complete_valid", "reasons": [],
                         "outcome_class": candidate["outcome_class"]}, configuration)
        require(type(repeat["broker_count"]) is int and repeat["statistics_reference"] is not None,
                configuration + " missing broker-count projection")
        require(not {"ok", "loss", "phantom", "reported_failure", "duplicate", "reorder"}
                .intersection(repeat), configuration + " has inapplicable ADR-012 counts")
    matches(receipt, {
        "schema_version": 1, "mode": "single-action-judge", "run_id": RUN_ID,
        "scenario_id": "F04", "configuration_id": configuration, "source_commit": SOURCE,
        "job_name": judge["job"], "pod_name": judge["pod"], "pod_uid": judge["pod_uid"],
        "candidate_name": f"{RUN_ID}/single-action/F04/{configuration}/candidate.json",
        "handler_exit_code": 0, "cleanup_completed": True,
    }, configuration + " judge receipt")


def derive(candidates):
    """Pinned expected.yaml: complete/deterministic and seed prerequisite checked above."""
    classes = {c: candidate["outcome_class"] for c, candidate in candidates.items()}
    require(all(value["origin"] != "construction" for value in classes.values()),
            "construction-origin outcome blocks this publication")
    require(all(classes[c]["result"] == "success" for c in ("C0", "C4")), "failed control")
    require(all(classes[c]["result"] in ("error", "no_outcome") for c in ("C1", "C2", "C3")),
            "undecided or successful hypothesis blocks this publication")
    require(classes["C1"]["result"] == classes["C3"]["result"] == "error",
            "C1/C3 must have a terminal outcome")
    derived = {"verdict": "undocumented", "hypotheses": {
        "H0": "holds", "H1": "recorded_only", "H2": "recorded_only",
        "H3": "recorded_only", "H4": "holds"}, "reason": None}
    require(all(candidates[c]["item_assessment"] is None for c in CONFIGURATIONS[:-1]),
            "early configuration carries an item assessment")
    require(candidates["C4"]["item_assessment"] == derived,
            "derived assessment differs from verifier item_assessment")
    return derived


def seed_receipt(pods, evidence):
    prefix = "PARCHWELL_F04_SEED_RECEIPT "
    lines = (pods / evidence["pod_log_member"]).read_text().splitlines()
    receipts = [json.loads(line[len(prefix):]) for line in lines if line.startswith(prefix)]
    require(len(receipts) == 1, "seed must have exactly one receipt")
    receipt = receipts[0]
    expected = {
        "schema_version": 1, "mode": "single-action-seed", "run_id": RUN_ID,
        "source_commit": SOURCE, "job_name": evidence["job"], "pod_name": evidence["pod"],
        "pod_uid": evidence["pod_uid"], "service_account": "enrichment-consumer",
        "topic": "document-state", "partition": 0,
        "key_base64": base64.b64encode(("f04-seed:" + RUN_ID).encode("ascii")).decode("ascii"),
        "value_sha256": sha256(b"s" * 1000), "delivered": True, "error": None,
        "handler_exit_code": 0, "cleanup_completed": True,
    }
    require(receipt == expected, "seed prerequisite receipt differs")
    return receipt


def build(candidates_dir, pods_dir, c5_path):
    for name, digest in SOURCE_HASHES.items():
        pinned(ROOT / name, digest)
    for name, digest in CANDIDATE_HASHES.items():
        pinned(candidates_dir / name, digest)
    for name, digest in POD_HASHES.items():
        pinned(pods_dir / name, digest)
    summary = json.loads(pinned(c5_path, C5_HASH))
    matches(summary, {"commit": C5_SOURCE, "run_id": "40642331044", "run_attempt": "1",
                      "measurement_complete": True, "incomplete_reasons": [],
                      "break_passed": True, "numerator": 1341, "denominator": 1341}, "C5")
    candidates, receipts, jobs = {}, {}, {}
    for configuration in CONFIGURATIONS:
        candidate = json.loads((candidates_dir / f"F04-{configuration}.json").read_bytes())
        receipt = json.loads((candidates_dir / f"F04-{configuration}-judge-completion.json").read_bytes())
        digests = candidate["image_digests"]
        job = f"{RUN_ID}-f04-{configuration.lower()}-single"
        account = "probe" if configuration in ("C2", "C4") else "loadgen"
        jobs[configuration] = {
            "capture": pod_evidence(pods_dir, job, account, digests["loadgen"], configuration),
            "judge": pod_evidence(pods_dir, job + "-judge", "verifier", digests["verifier"], configuration),
        }
        check_candidate(configuration, candidate, receipt, jobs[configuration]["judge"])
        candidates[configuration], receipts[configuration] = candidate, receipt
    require(len({json.dumps(c["image_digests"], sort_keys=True) for c in candidates.values()}) == 1,
            "configuration image digests differ")
    seed = pod_evidence(pods_dir, RUN_ID + "-f04-seed", "enrichment-consumer", digests["enrichment"])
    prerequisite = seed_receipt(pods_dir, seed)
    assessment = derive(candidates)
    c2_log = (pods_dir / jobs["C2"]["capture"]["pod_log_member"]).read_text()
    capped = re.findall(r"^single_action_client_log_capped r(\d+) dropped_lines (\d+)$", c2_log, re.M)
    require(capped == [(str(n), "667") for n in range(1, 6)], "C2 capped-log markers differ")
    expected_hash = SOURCE_HASHES["scenarios/F04/expected.yaml"]
    publication = {
        "item": "F04", "proposed_verdict": assessment["verdict"],
        "summary": [
            "Across 25 complete synthetic repeats in five deterministic configurations, both authorized controls succeeded and the missing-role producer returned broker error 29 (RD_KAFKA_RESP_ERR_TOPIC_AUTHORIZATION_FAILED), so the proposed F04 verdict is undocumented.",
            "The missing-role consumer recorded exactly 'no read and no error within the window' in all five repeats; no authorization conclusion is drawn, and its debug cause is unavailable because all five repeats capped client logging.",
            "The wrong-resource producer ended every repeat with local error -169 (RD_KAFKA_RESP_ERR__AUTHENTICATION); H1, H2 and H3 record observed surfaces only.",
        ],
        "hypotheses": assessment["hypotheses"],
        "derivation": "closed decision rules transcribed from scenarios/F04/expected.yaml (SHA-256 "
                      + expected_hash + "); derived assessment equals the verifier item_assessment",
        "per_configuration": {
            c: {"status": candidates[c]["status"], "deterministic": candidates[c]["deterministic"],
                "outcome_class": candidates[c]["outcome_class"],
                "broker_count_per_repeat": [r["broker_count"] for r in candidates[c]["repeats"]],
                "broker_count_lower_bound": True} for c in CONFIGURATIONS
        },
        "c2_observation": "no read and no error within the window",
        "c2_debug_cause": {"status": "unavailable", "reason": "single_action_client_log_capped",
                           "dropped_lines_per_repeat": [int(n) for _, n in capped],
                           "pod_log_member": jobs["C2"]["capture"]["pod_log_member"]},
        "error_code_names": {"29": "RD_KAFKA_RESP_ERR_TOPIC_AUTHORIZATION_FAILED",
                             "-169": "RD_KAFKA_RESP_ERR__AUTHENTICATION"},
        "error_code_source": "https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.h#L352-L495",
        "adr012_counts": "not_applicable",
        "run": {"workflow_run": RUN, "attempt": 1, "run_id": RUN_ID, "source_commit": SOURCE,
                "candidate_artifact": CANDIDATE_ARTIFACT, "pod_artifact": POD_ARTIFACT,
                "candidate_artifact_expires_at": "2027-01-26T16:03:35Z",
                "pod_artifact_expires_at": "2027-01-26T16:03:35Z", "jobs": jobs,
                "seed_job": seed},
        "seed_prerequisite": prerequisite,
        "input_sha256": {"source": SOURCE_HASHES, "candidate_artifact_members": CANDIDATE_HASHES,
                         "pod_artifact_members": POD_HASHES, "c5_summary": C5_HASH},
        "versions": {"confluent_kafka_managed": "2.16.0.0", "librdkafka_native": "2.16.0",
                     "image_digests": digests},
        "environment": {
            "region": "uksouth", "event_hubs_sku": "Premium", "pu_count": 1,
            "measurement_source": RUN + "/job/122935779352",
            "measurement_timestamps": {"capacity": "2026-10-28T16:57:19.2361202Z",
                                        "location": "2026-10-28T16:57:19.2371086Z",
                                        "sku": "2026-10-28T16:57:19.2387025Z"},
            "node_size": "Standard_D4ds_v5",
            "node_size_evidence": "inferred from the pinned infra/cluster/aks.tf setting for all three pools and the no-change cluster plan",
            "node_size_sources": [REPO + "/blob/" + SOURCE + "/infra/cluster/aks.tf", RUN + "/job/122935779352"],
            "cluster_plan_timestamp": "2026-10-28T16:13:38.8790298Z",
        },
        "mutation_gate": {"run": C5_RUN, "summary_artifact": C5_RUN + "/artifacts/12808596014",
                          "commit": summary["commit"], "measurement_complete": summary["measurement_complete"],
                          "break_passed": summary["break_passed"], "numerator": summary["numerator"],
                          "denominator": summary["denominator"],
                          "target_history": "C5 targets unchanged between measured commit and final-run source; checked in authored receipt"},
        "approval_sources": [REPO + "/issues/210#issuecomment-6415356278",
                             REPO + "/issues/210#issuecomment-6454346600",
                             REPO + "/issues/210#issuecomment-6477157262",
                             REPO + "/issues/230#issuecomment-6477157742"],
        "workload": "synthetic",
    }
    return {"publication": publication, "candidates": candidates, "judge_receipts": receipts}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--candidates", required=True, type=pathlib.Path)
    parser.add_argument("--pods", required=True, type=pathlib.Path)
    parser.add_argument("--c5-summary", required=True, type=pathlib.Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    try:
        output = json.dumps(build(args.candidates, args.pods, args.c5_summary), indent=2) + "\n"
        if args.check:
            require(TARGET.exists() and TARGET.read_bytes() == output.encode("utf-8"),
                    "generated output differs from " + str(TARGET))
        else:
            TARGET.write_text(output, encoding="utf-8")
    except (ValueError, KeyError, TypeError, OSError) as error:
        sys.exit("make_results: " + str(error))
    print("F04", sha256(output.encode("utf-8")))


if __name__ == "__main__":
    main()

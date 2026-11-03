"""Assemble the A03 publication from batch run 41534423570, offline.

Purpose: build results/A03/2026-11-02.json and results/A03/2026-11-02.md, the
candidate-derived result and report for the A03 verdict pull request (issue 321),
from the final idle-observation batch run 41534423570 and the mutation run
41484094658. Nothing here reaches Azure or GitHub.

Command line:

    python3 -I docs/operations/evidence/issue-321-a03-verdict/make_results.py \
        --candidates DIR --pods DIR --c5 DIR --batch-log FILE --stop-log FILE \
        [--check | --readme-row | --self-test]

Supported inputs: the extracted candidate and pod artifacts of attempt 1, the
extracted C5 summary artifact of run 41484094658, the two job logs, and this
checkout's git objects for 9014b8e, f0850d4 and 2dc9715.

Output: with no mode flag it writes only results/A03/2026-11-02.json and
results/A03/2026-11-02.md. It is not a second scientific validator. The
verifier's candidate stays authoritative; the transcribed rules only confirm it.

Failure conditions (exit 1, "FAIL G<n>: <message>" on stderr):
  G1  an input is missing, extra, or does not match its pinned SHA-256
  G2  the judge receipt does not match the run, pod, or candidate
  G3  the candidate identity, versions, digests, notes or keys differ
  G4  the outcome is not `confirmed`, `microsoft_value_kept_connection`
  G5  a configuration or repeat has the wrong shape, status or client ID
  G6  a recomputed class, summary, verdict, reason or hypothesis differs
  G7  the phase status or batch record differs
  G8  a pod, container, exit code, image digest or time differs
  G9  a pinned job-log line differs
  G10 the mutation summary, survivor set or recorded dispositions differ
  G11 a source file or a pinned source line differs
  G12 a recomputed quiet maximum, disconnect, Metadata step or count differs
  G13 (--check only) a generated file or README line 15 differs

Regression example: copy the candidates directory, append a newline to
A03-idle-candidate.json and run with --check: G1 fails before anything is written.
"""

import argparse
import hashlib
import json
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[4]

REPO = "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet"
RUN = REPO + "/actions/runs/41534423570"
RUN_ID = "idle-41534423570-1"
SOURCE = "9014b8effd4f8556d3446bcfbf8f951a52905bb2"
MUTATION_COMMIT = "f0850d44086c4065cad76e64da6ec6f2d9cc50ce"
C5_RUN = REPO + "/actions/runs/41484094658"
PRIOR_C5_COMMIT = "2dc9715e3de176f7e1cd55eee9c251e43c29c746"
LOADGEN_DIGEST = "sha256:7977c7653a3fe7c667eb4bba17b55e975116da105cb7d003dd1ea11fee2bd35f"
VERIFIER_DIGEST = "sha256:580a54a111c6ad496ffabf9a981a8a5a82607f6cc85f4322a9d7e964677958a8"
CONFIGS = ("C0", "C1", "C2")
PODS = {
    "C0": ("idle-41534423570-1-a03-c0-idle", "idle-41534423570-1-a03-c0-idle-7279r", "7672f9f0-3bb4-40e9-bc79-40c94f2f7a71", "loadgen"),
    "C1": ("idle-41534423570-1-a03-c1-idle", "idle-41534423570-1-a03-c1-idle-kf8st", "8540572d-823d-4b00-95ef-b96dd5fcdcf0", "loadgen"),
    "C2": ("idle-41534423570-1-a03-c2-idle", "idle-41534423570-1-a03-c2-idle-pjvnf", "fa1ed6b9-b24d-45b1-9bb2-c2a83be847f0", "loadgen"),
    "judge": ("idle-41534423570-1-idle-judge", "idle-41534423570-1-idle-judge-fmw2v", "84047dc6-90d1-4118-ad60-304c9401465a", "verifier"),
}
TIMERS = {"C0": ("900000", "300000"), "C1": ("180000", "300000"), "C2": ("180000", "180000")}
CONTAINER_TIMES = {
    "C0": ("2026-11-02T22:44:17Z", "2026-11-02T22:51:40Z"),
    "C1": ("2026-11-02T22:51:52Z", "2026-11-02T22:59:13Z"),
    "C2": ("2026-11-02T22:59:20Z", "2026-11-02T23:06:42Z"),
    "judge": ("2026-11-02T23:23:02Z", "2026-11-02T23:23:07Z"),
}
REPEAT_MAX_QUIET = {
    "C0": [269764, 269780, 269777, 269737, 269728],
    "C1": [179862, 179781, 179834, 179803, 179901],
    "C2": [165008, 165009, 165009, 165009, 165010],
}
# (configuration, repeat, report sequence, disconnects added, quiet ms in the previous report, req_timeouts added)
DISCONNECTS = [
    ("C0", 1, 19, 1, 269764, 0), ("C0", 2, 19, 1, 269780, 0), ("C0", 3, 19, 1, 269777, 0),
    ("C0", 4, 19, 1, 269737, 0), ("C0", 5, 19, 1, 269728, 0),
]
# (configuration, repeat, report sequence, report observed_elapsed_ms, Metadata requests added)
METADATA_STEPS = [
    ("C0", 1, 20, 300067, 1), ("C0", 2, 20, 300016, 1), ("C0", 3, 20, 300016, 1), ("C0", 4, 20, 300015, 1), ("C0", 5, 20, 300015, 1),
    ("C1", 1, 13, 195063, 1), ("C1", 1, 20, 300067, 1), ("C1", 2, 13, 195012, 1), ("C1", 2, 20, 300016, 1),
    ("C1", 3, 13, 195012, 1), ("C1", 3, 20, 300015, 1), ("C1", 4, 13, 195012, 1), ("C1", 4, 20, 300016, 1),
    ("C1", 5, 13, 195011, 1), ("C1", 5, 20, 300014, 1),
    ("C2", 1, 12, 180062, 1), ("C2", 1, 24, 360068, 1), ("C2", 2, 12, 180011, 1), ("C2", 2, 24, 360016, 1),
    ("C2", 3, 12, 180011, 1), ("C2", 3, 24, 360018, 1), ("C2", 4, 12, 180012, 1), ("C2", 4, 24, 360018, 1),
    ("C2", 5, 12, 180012, 1), ("C2", 5, 24, 360018, 1),
]
IDLE_METADATA = {"C0": 1, "C1": 2, "C2": 2}

PINS = {
    "cand/A03-idle-candidate.json": "2c1c9a8fcbda1f9d77b17ec0e339d6a12ea64b23867f910fa7f55be5eb140b22",
    "cand/idle-judge-completion.json": "88986954508a2b92cd72bfac574350b66ee57655412225c6904f93aaee3061cb",
    "pods/phase-status.json": "11eadcceba059132dd414f89e983a5bcaa191814e8dbe53f4c731939ad4a1641",
    "pods/idle-observation-batch.json": "022df5431aea016dd3b1b0b891e2f28a13023ccd3fc0a6ea72f5d22c290bdb67",
    "pods/idle-41534423570-1-a03-c0-idle/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "pods/idle-41534423570-1-a03-c0-idle/idle-41534423570-1-a03-c0-idle-7279r.containers.loadgen.log": "94e5d52abf88c3e8551d2e714b461135a7ed585fb8067d42873aebcbf2f256ba",
    "pods/idle-41534423570-1-a03-c0-idle/pods.json": "9caac5ed4292728d9e998a12a6458757c110bdabe3bc1b8611d520d190c9534e",
    "pods/idle-41534423570-1-a03-c1-idle/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "pods/idle-41534423570-1-a03-c1-idle/idle-41534423570-1-a03-c1-idle-kf8st.containers.loadgen.log": "56a94df8edab24c34dd55ffa2dad6341b6c8e37fa7858065fccd394ed36d4461",
    "pods/idle-41534423570-1-a03-c1-idle/pods.json": "5e1310550a19045525de7969a1f9358f998d2b737528115d94d3a683da985727",
    "pods/idle-41534423570-1-a03-c2-idle/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "pods/idle-41534423570-1-a03-c2-idle/idle-41534423570-1-a03-c2-idle-pjvnf.containers.loadgen.log": "b8de1990bac6f06d3e060f5297aba2500203832832dac5c24465f3a70ef19b8e",
    "pods/idle-41534423570-1-a03-c2-idle/pods.json": "4cce58bfd5f9f7071cb28ab163c53e2dc7aa9fd1b8138d747776c187d2a8428e",
    "pods/idle-41534423570-1-idle-judge/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "pods/idle-41534423570-1-idle-judge/idle-41534423570-1-idle-judge-fmw2v.containers.verifier.log": "c254c80f0d3edcccce4a7fe6770ed9ba799d740d5dad4f2ca44b69f934d3c5aa",
    "pods/idle-41534423570-1-idle-judge/pods.json": "4ba53d9667d70b5c3bfbacaa4609530d12d65b343cef0199e7c509d4b760298c",
    "c5/summary.json": "32b6613b4818780be973e7cf85feb4329927aa590700a3a477854b5f5dd817a3",
    "c5/summary.md": "1421bc0efefbd9d408dc444694b64b0321291bb24dfa2aa78264f167c7b2dda3",
    "c5/survivors.json": "220e4b887e882e92ee40472b81c5ceedd735a4b0fc5ef5ab1f89143e323693e6",
    "logs/batch.log": "73e59b9f58fef0642e9e1166b98a8e0d15072badbd283a0c48eff36b233e9f22",
    "logs/stop.log": "5cf7ed3214b29335a820af670696e41c7ba0c01ea7dd6e805f9689e01af0f583",
}

SOURCES = {
    "scenarios/A03/scenario.yaml": "083e21240d442078d2c3383249a5539aa8fc309ee5697513656916005eddb7dc",
    "scenarios/A03/expected.yaml": "ba82a28a2085d1cc27c44f448162353d23f517b1b6200b677d4d334f75b833f4",
    "infra/cluster/aks.tf": "969762e923eae813525207d50f5689de9fbed20c0c4c2c274367bcb54462a8a2",
    "src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs": "a1395a1ab548ec97c42a04af73051987da114bc921d08babc2a2d6a91fe51420",
}
WORKING_TREE_SOURCES = ("scenarios/A03/scenario.yaml", "scenarios/A03/expected.yaml")

# (population, target, Killed, Timeout, Survived, NoCoverage, CompileError, RuntimeError, Ignored, numerator, denominator)
TARGETS = [
    ("phase", "Judge/PhaseJudge.cs", 189, 1, 16, 0, 4, 0, 6, 190, 206),
    ("publication", "VerifyRun/Publication/PublicationAcceptance.cs", 177, 0, 6, 0, 6, 0, 14, 177, 183),
    ("a01", "VerifyRun/A01/A01EvidenceRecorder.cs", 71, 0, 0, 0, 15, 0, 8, 71, 71),
    ("workload", "VerifyRun/A01/WorkloadRecorder.cs", 95, 0, 0, 0, 2, 0, 7, 95, 95),
    ("single-action-repeat", "VerifyRun/SingleAction/SingleActionRepeatJudgement.cs", 792, 0, 8, 0, 142, 0, 29, 792, 800),
    ("single-action-configuration", "VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs", 2003, 0, 8, 0, 158, 0, 46, 2003, 2011),
    ("idle-observation", "VerifyRun/IdleObservation/IdleObservationJudgement.cs", 424, 0, 0, 0, 101, 0, 14, 424, 424),
]
STATUS_NAMES = ("Killed", "Timeout", "Survived", "NoCoverage", "CompileError", "RuntimeError", "Ignored")
COMBINED = (3751, 1, 38, 0, 428, 0, 124, 3752, 3790)

TARGET_ABBREVIATIONS = {
    "PJ": "Judge/PhaseJudge.cs",
    "PA": "VerifyRun/Publication/PublicationAcceptance.cs",
    "SAC": "VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs",
    "SAR": "VerifyRun/SingleAction/SingleActionRepeatJudgement.cs",
}
SECTIONS = {
    "S275": ("Issue 275", "issue-275-survivor-dispositions"),
    "S281": ("Issue 281", "issue-281-survivor-dispositions"),
    "S283": ("Issue 283", "issue-283-survivor-dispositions"),
}
# mutant id, target, start line, mutatorName, recorded disposition, proof, section
SURVIVORS = [
    (4992, "PJ", 76, "Logical mutation", "equivalent", "E1", "S275"),
    (4994, "PJ", 76, "Linq method mutation (Any() to All())", "equivalent", "E1", "S275"),
    (4995, "PJ", 76, "Equality mutation", "equivalent", "E1", "S275"),
    (4998, "PJ", 76, "Statement mutation", "equivalent", "E1", "S275"),
    (5013, "PJ", 80, "Linq method mutation (All() to Any())", "equivalent", "E2", "S275"),
    (5014, "PJ", 80, "Logical mutation", "equivalent", "E2", "S275"),
    (5015, "PJ", 80, "Logical mutation", "equivalent", "E2", "S275"),
    (5016, "PJ", 80, "Logical mutation", "equivalent", "E2", "S275"),
    (5017, "PJ", 80, "Logical mutation", "equivalent", "E2", "S275"),
    (5018, "PJ", 80, "Logical mutation", "equivalent", "E2", "S275"),
    (5028, "PJ", 86, "Remove checked expression", "equivalent", "E3", "S275"),
    (5030, "PJ", 86, "Remove checked expression", "equivalent", "E3", "S275"),
    (5032, "PJ", 86, "Remove checked expression", "equivalent", "E3", "S275"),
    (5034, "PJ", 86, "Remove checked expression", "equivalent", "E3", "S275"),
    (5036, "PJ", 86, "Remove checked expression", "equivalent", "E3", "S275"),
    (5038, "PJ", 86, "Remove checked expression", "equivalent", "E3", "S275"),
    (10892, "PA", 19, "String mutation", "killed on replay", "R2", "S275"),
    (10894, "PA", 19, "String mutation", "killed on replay", "R2", "S275"),
    (10896, "PA", 20, "String mutation", "killed on replay", "R1", "S275"),
    (10898, "PA", 20, "String mutation", "killed on replay", "R1", "S275"),
    (10900, "PA", 20, "String mutation", "killed on replay", "R1", "S275"),
    (10982, "PA", 61, "Statement mutation", "equivalent", "E4", "S275"),
    (12897, "SAC", 254, "Statement mutation", "equivalent", "EP1", "S281"),
    (13251, "SAC", 346, "String mutation", "equivalent", "EP2", "S281"),
    (13965, "SAC", 523, "String mutation", "equivalent", "EQ1", "S283"),
    (13969, "SAC", 524, "String mutation", "equivalent", "EQ2", "S283"),
    (13970, "SAC", 524, "String mutation", "equivalent", "EQ2", "S283"),
    (13971, "SAC", 524, "String mutation", "equivalent", "EQ2", "S283"),
    (14015, "SAC", 536, "Equality mutation", "equivalent", "EQ3", "S283"),
    (14018, "SAC", 536, "String mutation", "equivalent", "EQ3", "S283"),
    (14421, "SAR", 62, "Boolean mutation", "equivalent", "E5", "S275"),
    (14504, "SAR", 101, "String mutation", "equivalent", "E6", "S275"),
    (14810, "SAR", 175, "Boolean mutation", "equivalent", "E7", "S275"),
    (15097, "SAR", 245, "Logical mutation", "equivalent", "E8", "S275"),
    (15103, "SAR", 246, "Linq method mutation (First() to FirstOrDefault())", "equivalent", "E9", "S275"),
    (15167, "SAR", 277, "String mutation", "equivalent", "E6", "S275"),
    (15260, "SAR", 299, "Logical mutation", "equivalent", "E10", "S275"),
    (15262, "SAR", 299, "Block removal mutation", "equivalent", "E11", "S275"),
]

DECISION = "over the survival in assessment_rules: C0 kept and C1 not_kept -> null, `inverse_effect`. Else C1 not_kept -> contradicted, `closed_with_microsoft_value`. Else C0 not_kept -> confirmed, `microsoft_value_kept_connection`. Else (both kept) -> untestable, `no_idle_close_observed`."
PRECEDENCE = "smoke run -> null, `smoke_only`; a missing candidate or any configuration not `complete_valid` -> null, `incomplete_configuration`; any repeat `first_failed` -> `untestable`, `first_produce_failed`; any configuration whose five repeats do not share one survival -> null, `unequal_classes`; then the item rule."
EXPECTED_LINES = [
    '    decision: "' + DECISION + '"',
    '  decision: "' + DECISION + '"',
    '  precedence: "' + PRECEDENCE + '"',
    '    expected: "not_kept in C1 as in C0, an inference from rdkafka.c L2458-L2461"',
    '    reading: "the note \\"Must be < 240000\\" with \\"Azure closes idle connections after 240 seconds\\" is read as a claim that the value avoids idle closure"',
    '  text_must_state: "the 225000 ms idle_timed threshold is a harness choice, not a measurement; per repeat, the other_cause disconnects, request timeouts, second produce outcome and client error callbacks."',
]
SCENARIO_LINES = [
    "      metadata.max.age.ms: 900000",
    "  C1: {}",
    "      topic.metadata.refresh.interval.ms: 180000",
    "  idle_seconds: 420",
]
BASELINE_LINES = [
    '        KafkaSettingDefinition.Explicit(KafkaSettingKeys.MetadataMaxAgeMs, "180000", ["A03"], [MicrosoftGuidance],',
    '        KafkaSettingDefinition.Explicit(KafkaSettingKeys.TopicMetadataRefreshIntervalMs, "300000", ["A03"], [NativeConfiguration],',
]

CLASSES = ("kept", "closed_while_idle", "stale_on_resume", "first_failed")
HYPOTHESES = ("H_metadata_age", "H_metadata_cadence", "H_refresh_interval")
WINDOW_KEYS = (
    "idle_timed_disconnects", "idle_other_cause_disconnects", "idle_connects", "idle_metadata_requests",
    "idle_request_timeouts", "first_close_after_idle_ms", "first_close_quiet_ms", "resume_idle_timed_disconnects",
    "resume_other_cause_disconnects", "resume_connects", "resume_request_timeouts",
)
NULLABLE_WINDOW_KEYS = ("first_close_after_idle_ms", "first_close_quiet_ms")
REPEAT_KEYS = (
    "repeat", "client_id", "status", "reasons", "connection_class", "first", "second", "windows",
    "maximum_up_brokers", "reports", "client_errors", "client_errors_dropped",
)
PRODUCE_KEYS = ("result", "error_code", "is_fatal", "origin", "latency_ms")
ERROR_ROW_KEYS = ("code", "is_fatal", "origin", "observed_elapsed_ms")
CONFIG_KEYS = ("configuration_id", "status", "summary", "capture_sha256", "repeats")
CANDIDATE_KEYS = (
    "schema_version", "mode", "synthetic", "run_kind", "run_id", "scenario_id", "source_commit", "managed_version",
    "native_version", "image_digests", "configurations", "item_assessment", "adr012_counts", "notes",
)
RECEIPT_KEYS = (
    "schema_version", "mode", "run_id", "source_commit", "job_name", "pod_name", "pod_uid", "candidates",
    "handler_exit_code", "cleanup_completed",
)
MUTATION_DOC = "../../docs/operations/verifier-mutation.md"

TEMPLATE = "\n".join([
    "# A03: producer connections were kept across a 420 s idle period with `metadata.max.age.ms=180000`, and not with 900000",
    "",
    "In batch run 41534423570 on Event Hubs Premium, five fresh synthetic producers per configuration each produced one record, stayed idle for 420 s and produced again: C0 with `metadata.max.age.ms=900000`, the default in Microsoft's table, C1 with `180000`, Microsoft's recommendation, and C2 with `180000` and `topic.metadata.refresh.interval.ms=180000`; C0 and C1 left the refresh interval at the baseline 300000 ms. Counting only disconnects after a quiet period of at least 225000 ms, a harness threshold rather than a measurement, C0's connections were not kept in all five repeats (@@C0_CLASSES@@) and C1's were kept in all five (@@C1_CLASSES@@), so the expected.yaml rules select `confirmed`, reason `microsoft_value_kept_connection`. The expected outcome, inferred from the librdkafka source, was that C1 would not be kept either; that difference is reported below as a finding.",
    "",
    "Proposed verdict: `confirmed`.",
    "",
    "## Approved inputs and run",
    "",
    "The [approved scenario](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/9014b8effd4f8556d3446bcfbf8f951a52905bb2/scenarios/A03/scenario.yaml) and [expected outcome](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/9014b8effd4f8556d3446bcfbf8f951a52905bb2/scenarios/A03/expected.yaml) are unchanged on the `main` this pull request is based on. Their decision rules were approved with the [idle observation judgement plan, Revision 6](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/319#issuecomment-6575747094) and its [pinned-code part](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/319#issuecomment-6579253743) by [plan check 6579727682](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/319#issuecomment-6579727682) under standing approval, which the [override decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563515099), point 2, allows, and merged in [pull request 328](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/328) at 2026-11-02T16:39:43Z as `f0850d44086c4065cad76e64da6ec6f2d9cc50ce`, after the unpublished smoke run 41410644841, as the [split decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563452228), point 4, requires. [Pull request 330](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/330), merged at 2026-11-02T22:06:44Z as `9014b8effd4f8556d3446bcfbf8f951a52905bb2` before this run was created, changed no A03 file, so the A03 scenario and expected outcome are byte-identical at both commits.",
    "",
    "This report uses only [batch run 41534423570](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41534423570), attempt 1, event `workflow_dispatch`, on `main` at `9014b8effd4f8556d3446bcfbf8f951a52905bb2`, created 2026-11-02T22:07:00Z, conclusion `success`, as the [A03 publication decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/321#issuecomment-6596020684) requires: it is the final batch run that included A03, and nothing is published for A03 from the earlier run 41483886512. Under the [verdict and bypass decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563540913), point 1, the verdict word is the one the merged decision rules select. The evidence is in two artifacts, both retained until 2027-01-31T22:07:02Z:",
    "",
    "- [candidate artifact 13187681970](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41534423570/artifacts/13187681970) (`idle-observation-candidates-41534423570-1`), ZIP SHA-256 `9e2376e4fb39f2491f931b217d574db2ad325f52e457f5709d330be82ed186ec`;",
    "- [pod artifact 13188401369](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41534423570/artifacts/13188401369) (`idle-observation-pods-41534423570-1`), ZIP SHA-256 `35a992329edcd87dfd1d84f636228f1ff417791dd8626c4df823db597404df42`.",
    "",
    "The job logs are [batch job 125582551893](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41534423570/job/125582551893) (log SHA-256 `73e59b9f58fef0642e9e1166b98a8e0d15072badbd283a0c48eff36b233e9f22`) and [stop job 125619643005](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41534423570/job/125619643005) (log SHA-256 `5cf7ed3214b29335a820af670696e41c7ba0c01ea7dd6e805f9689e01af0f583`). The [candidate-derived result](2026-11-02.json) holds the A03 candidate and the judge receipt, equal in content to the artifact members `A03-idle-candidate.json` and `idle-judge-completion.json` and re-serialized with indentation. The generator [make_results.py](../../docs/operations/evidence/issue-321-a03-verdict/make_results.py) pins the SHA-256 of every input and wrote this report and that file; the [authored receipt](../../docs/operations/evidence/issue-321-a03-verdict/receipt.md) describes it.",
    "",
    "Each repeat is one fresh producer. The five producers of a configuration ran at the same time in one pod. Each produced one 1000-byte synthetic record to topic `document-events`, partition 0, stayed idle for 420 s with nothing to send while its callbacks were served, and then produced a second record, as the [idle-observation contract](../../docs/operations/idle-observation-contract.md) describes. C1 keeps the harness baseline: `metadata.max.age.ms` 180000 and `topic.metadata.refresh.interval.ms` 300000. C0 sets `metadata.max.age.ms` to 900000, and C2 sets `topic.metadata.refresh.interval.ms` to 180000. Nothing else differs; in particular `socket.keepalive.enable` is the baseline `true` in all three, with the node's keepalive timers unchanged. Every repeat is `complete_valid`, so its captured settings equal the resolved baseline plus the scenario's selection (the `idle_settings` check of the verifier contract).",
    "",
    "## Repeat outcomes",
    "",
    "The verifier judged every repeat under the [idle-observation judgement](../../docs/operations/verifier-contract.md#idle-observation-judgement). A disconnect is classed only by its quiet period: the smaller of `txidle` and `rxidle` in the statistics report before the disconnect. It counts as `idle_timed` when that period is at least 225000 ms. That threshold is a harness choice, not a measurement: Microsoft's stated 240000 ms idle limit minus one 15000 ms report interval. No report names who closed a connection. A repeat is `closed_while_idle` when an `idle_timed` disconnect falls in the idle window, `stale_on_resume` when one falls only in the resume window, and `kept` otherwise; the first two are `not_kept`.",
    "",
    "<!-- tables:configurations -->",
    "",
    "<!-- tables:repeat-outcomes -->",
    "",
    "The idle window runs from the first report at or after idle start to the last report at or before idle end. The resume window runs from there to the first report at or after the second produce's outcome. Per expected.yaml, `other_cause` disconnects, request timeouts, the second produce's outcome and client error callbacks are recorded per repeat below and select nothing. The idle window's Metadata requests are the recorded-only hypothesis H_metadata_cadence.",
    "",
    "<!-- tables:windows -->",
    "",
    "<!-- tables:client-errors -->",
    "",
    "ADR-012 verifier counts (`ok`, `loss`, `phantom`, `reported_failure`, `duplicate` and `reorder`) are not applicable to this item and are absent, not zero, as `assessment_rules.adr012_counts` in expected.yaml states. The broker count of each client is `maximum_up_brokers`: the largest number, over its statistics reports, of brokers with source `learned`, `configured` or `logical` and state `UP`. It is in the repeat outcomes table, and the window tables give the `connects` deltas.",
    "",
    "## Assessment against expected.yaml",
    "",
    "Microsoft's [client configuration page](https://github.com/MicrosoftDocs/azure-docs/blob/6da87e3c59251e962dd0b37dbd99ae364d767150/articles/event-hubs/apache-kafka-configurations.md) lists `metadata.max.age.ms` for librdkafka with default 900000, recommended value 180000, the constraint \"Must be < 240000\" and the note \"Azure closes idle connections after 240 seconds\" (line 101). The line was read on 2026-11-03 at that page revision, whose front matter has `ms.date: 12/17/2025`, and the librdkafka table of the [live page](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations), read the same day, has the same row. expected.yaml reads the constraint with the note \"as a claim that the value avoids idle closure\".",
    "",
    "The precedence in expected.yaml is: \"@@PRECEDENCE@@\" The item rule is: \"@@DECISION@@\"",
    "",
    "<!-- tables:hypotheses -->",
    "",
    "**H_metadata_age: `holds`.** All three configurations are `complete_valid`, no repeat is `first_failed`, and the five repeats of each configuration share one survival, so the precedence passes to the item rule. C1 is `kept` and C0 is `not_kept`, so the item rule gives `confirmed`, reason `microsoft_value_kept_connection`. H_metadata_cadence is recorded only: the idle window's Metadata requests per repeat are in the windows table. H_refresh_interval is recorded only: C2 was `@@C2_SUMMARY@@` (@@C2_CLASSES@@).",
    "",
    "The generator holds these rules, transcribed from the pinned expected.yaml. It recomputes every connection class from the window counts, each configuration summary from the classes and the verdict from the summaries, and requires each to equal the verifier's candidate.",
    "",
    "## Finding: the result differs from the expected outcome",
    "",
    "expected.yaml expects `not_kept` in C1 as in C0, an inference from `src/rdkafka.c` L2458-L2461 at librdkafka v2.16.0, where the periodic metadata refresh timer is started from `topic.metadata.refresh.interval.ms`. C0 was `not_kept`, as expected. C1 was `kept` in all five repeats. Under AGENTS.md \"Verdict integrity\" this difference is reported as a finding; the expected outcome is not changed.",
    "",
    "The generator recomputes, from every statistics report in the candidate, the largest quiet period on any broker in each repeat, in whole milliseconds as the verifier computes it: @@MAX_C0_MIN@@ ms to @@MAX_C0_MAX@@ ms in C0, @@MAX_C1_MIN@@ ms to @@MAX_C1_MAX@@ ms in C1 and @@MAX_C2_MIN@@ ms to @@MAX_C2_MAX@@ ms in C2. Only C0's are above the 225000 ms threshold. In each C0 repeat the one disconnect followed a quiet period of @@CLOSE_MIN@@ ms to @@CLOSE_MAX@@ ms, with no request timeout, and no C1 or C2 repeat recorded a disconnect.",
    "",
    "<!-- tables:disconnects -->",
    "",
    "The generator also lists every rise of a broker's `req.Metadata` counter between consecutive reports after the first. Each C0 repeat shows one Metadata request, in report @@C0_META_REPORTS@@. Each C1 repeat shows two, in reports @@C1_META_REPORTS@@, and each C2 repeat two, in reports @@C2_META_REPORTS@@. The report time is the client's elapsed time when the report was observed; each request was sent after the report before it and before this one. In C1 the request first seen in report 13 does not line up with the 300000 ms refresh interval, which C0 and C1 share. Inferred, not established at the pinned source: that request followed the expiry of cached metadata under `metadata.max.age.ms` 180000, and it kept the connection that carried it from reaching 225000 ms of quiet. Which code path sent each request is not recorded.",
    "",
    "<!-- tables:metadata-requests -->",
    "",
    "## Not tested",
    "",
    "Not tested: consumers, idle periods other than 420 s, other values of either metadata setting, `metadata.max.age.ms` left unset, `socket.keepalive.enable=false` or any keepalive timer change, clients other than librdkafka 2.16.0 through Confluent.Kafka 2.16.0.0, tiers other than Premium, and which side closed a connection. This result does not show whether Event Hubs would have closed a C1 connection with no Metadata request inside the idle period.",
    "",
    "## Versions, environment and timing",
    "",
    "Confluent.Kafka `2.16.0.0`, from the candidate's `managed_version`, and librdkafka `2.16.0`, from its `native_version`. Image digests: loadgen `sha256:7977c7653a3fe7c667eb4bba17b55e975116da105cb7d003dd1ea11fee2bd35f` and verifier `sha256:580a54a111c6ad496ffabf9a981a8a5a82607f6cc85f4322a9d7e964677958a8`, each also in its pod's `imageID`. The workload is synthetic.",
    "",
    "The batch job log records the planned ephemeral namespace as capacity 1 (line 1436), location `uksouth` (line 1445) and SKU `Premium` (line 1451), so the namespace used 1 PU in UK South. Node size `Standard_D4ds_v5` is inferred from all three pools of the [pinned cluster configuration](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/9014b8effd4f8556d3446bcfbf8f951a52905bb2/infra/cluster/aks.tf) and the cluster plan reporting no changes at 2026-11-02T22:23:17Z (line 940); no node SKU was captured directly. The ephemeral state was empty at 2026-11-02T23:25:51.0606811Z (batch log line 4027) and the AKS cluster was stopped at 2026-11-02T23:27:03.6048502Z (stop log line 343).",
    "",
    "<!-- tables:jobs -->",
    "",
    "The members are in pod artifact 13188401369. Each client ID is the pod name followed by `-r<repeat>`. The pod logs carry no line tagged by repeat, because the five clients share one pod. The per-repeat evidence is the candidate's repeat results, which keep every projected statistics report, and each pod JSON records the worker's exit code 0.",
    "",
    "## Mutation publication gate",
    "",
    "Current gate: [C5 run 41484094658](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41484094658), attempt 1, measured `main` at `f0850d44086c4065cad76e64da6ec6f2d9cc50ce`. This batch run's source, `9014b8effd4f8556d3446bcfbf8f951a52905bb2`, descends from that commit, and all seven configured targets are byte-identical at both commits, so no later change modified a configured target. Its [summary artifact 13170506349](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41484094658/artifacts/13170506349) (`c5-summary-41484094658-1`), member `summary.json`, records `measurement_complete: true`, no incomplete reasons and `break_passed: true`, across all 14 shards of the seven configured targets. The break threshold is 60, from `thresholds.break` in `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` at that commit. The raw score is 99.0%, from 3752 / 3790 * 100 rounded to one decimal place. This meets the publication requirement in [verifier-mutation.md](../../docs/operations/verifier-mutation.md#issue-319-idle-observation-target), \"Issue 319 idle-observation target\".",
    "",
    "<!-- tables:mutation-targets -->",
    "",
    "`VerifyRun/IdleObservation/IdleObservationJudgement.cs`, the target that judged this item, has no survivor. The other 38 survivors are in four other targets, each byte-identical at `2dc9715e3de176f7e1cd55eee9c251e43c29c746`, which C5 run 41232286130 measured, and at `f0850d44086c4065cad76e64da6ec6f2d9cc50ce`. For each survivor, the table gives the disposition already recorded in [verifier-mutation.md](../../docs/operations/verifier-mutation.md) for a survivor of run 41232286130 with the same target, line and mutator, and the section that records it. Mutant IDs differ between the two runs. No replay was run for this pull request, so these are the earlier dispositions of the same source locations, not new ones. All 38 survivors are in the survivor sample, under its cap of 50, and `unreviewed_beyond_cap` is 0. `rebalance_needed` lists shard `single-action-configuration-p1` at 7385 s, over the 7200 s line; that is recorded only and changes neither field above.",
    "",
    "<!-- tables:survivors -->",
])

TABLE_HEADERS = {
    "configurations": [
        "| Configuration | `metadata.max.age.ms` | `topic.metadata.refresh.interval.ms` | Status | Summary | Capture SHA-256 |",
        "| --- | ---: | ---: | --- | --- | --- |",
    ],
    "repeat-outcomes": [
        "| Configuration | Repeat | Status | Connection class | First produce | First error code | First latency (ms) | Second produce | Second error code | Second latency (ms) | Max UP brokers |",
        "| --- | ---: | --- | --- | --- | ---: | ---: | --- | ---: | ---: | ---: |",
    ],
    "windows": [
        "| Configuration | Repeat | Idle idle_timed | Idle other_cause | Idle connects | Idle Metadata requests | Idle request timeouts | First close after idle (ms) | First close quiet (ms) | Resume idle_timed | Resume other_cause | Resume connects | Resume request timeouts | Client errors | Client errors dropped |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ],
    "client-errors": [
        "| Configuration | Repeat | Code | Fatal | Origin | Observed elapsed (ms) |",
        "| --- | ---: | ---: | --- | --- | ---: |",
    ],
    "hypotheses": ["| Hypothesis | Status |", "| --- | --- |"],
    "disconnects": [
        "| Configuration | Repeat | Largest quiet period (ms) | Disconnect report | Quiet before disconnect (ms) | Request timeouts added |",
        "| --- | ---: | ---: | ---: | ---: | ---: |",
    ],
    "metadata-requests": [
        "| Configuration | Repeat | Report | Report time (ms) | Metadata requests added |",
        "| --- | ---: | ---: | ---: | ---: |",
    ],
    "jobs": [
        "| Configuration | Stage | Job (account) | Container started | Container finished | Pod log member | Pod JSON member |",
        "| --- | --- | --- | --- | --- | --- | --- |",
    ],
    "mutation-targets": [
        "| Target | Killed | Timeout | Survived | NoCoverage | CompileError | RuntimeError | Ignored | Detected / scored |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ],
    "survivors": [
        "| Mutant | Target | Line | Mutator | Recorded disposition | Proof | Recorded in |",
        "| ---: | --- | ---: | --- | --- | --- | --- |",
    ],
}

OBSERVED = "With Microsoft's `metadata.max.age.ms=180000`, producers kept their connections across a 420 s idle period in five repeats (@@C1_CLASSES@@); with the default 900000, none did (@@C0_CLASSES@@). Only disconnects after at least 225000 ms of quiet count. The expected outcome, inferred from the librdkafka source, was `not_kept` with 180000 too; in the idle period each 180000 ms producer sent two Metadata requests and each 900000 ms producer one."


class CheckFailure(Exception):
    def __init__(self, code, message):
        super().__init__(message)
        self.code = code
        self.message = message


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def fail(code, message):
    raise CheckFailure(code, message)


def expect(condition, code, message):
    if not condition:
        fail(code, message)


def strict_json(raw):
    """Decode JSON, refusing duplicate keys and non-finite numbers."""
    def pairs(items):
        result = {}
        for key, item in items:
            if key in result:
                raise ValueError("duplicate JSON key " + key)
            result[key] = item
        return result

    def constant(_value):
        raise ValueError("non-finite JSON number")

    return json.loads(raw.decode("utf-8", errors="strict"), object_pairs_hook=pairs, parse_constant=constant)


def parse(inputs, key, code):
    try:
        return strict_json(inputs[key])
    except (ValueError, KeyError, UnicodeDecodeError) as error:
        fail(code, "cannot parse " + key + ": " + str(error))


def exact_keys(value, keys, code, label):
    expect(isinstance(value, dict), code, label + " is not an object")
    expect(sorted(value) == sorted(keys), code, label + " keys differ: " + ",".join(sorted(value)))


def is_int(value):
    return isinstance(value, int) and not isinstance(value, bool)


def load(args):
    inputs = {}
    bases = {"cand": pathlib.Path(args.candidates), "pods": pathlib.Path(args.pods), "c5": pathlib.Path(args.c5)}
    for key in PINS:
        if key == "logs/batch.log":
            path = pathlib.Path(args.batch_log)
        elif key == "logs/stop.log":
            path = pathlib.Path(args.stop_log)
        else:
            group, name = key.split("/", 1)
            path = bases[group] / name
        if path.is_file():
            inputs[key] = path.read_bytes()
    listing = sorted(entry.name for entry in bases["cand"].iterdir()) if bases["cand"].is_dir() else []
    for name in listing:
        if name.endswith("-idle-candidate.json") and ("cand/" + name) not in inputs:
            inputs["cand/" + name] = (bases["cand"] / name).read_bytes()
    inputs["cand-listing"] = json.dumps(listing).encode()
    return inputs


def git_show(path, commit=SOURCE, code="G11"):
    result = subprocess.run(["git", "-C", str(ROOT), "show", commit + ":" + path], capture_output=True)
    if result.returncode != 0:
        fail(code, "git show " + commit + ":" + path + " failed")
    return result.stdout


def git_rev(path, commit):
    result = subprocess.run(["git", "-C", str(ROOT), "rev-parse", commit + ":" + path], capture_output=True, text=True)
    if result.returncode != 0:
        fail("G10", "git rev-parse " + commit + ":" + path + " failed")
    return result.stdout.strip()


def is_ancestor(older, newer):
    return subprocess.run(["git", "-C", str(ROOT), "merge-base", "--is-ancestor", older, newer]).returncode == 0


def check_g1(inputs):
    listing = json.loads(inputs["cand-listing"])
    expected = ["A02-idle-candidate.json", "A03-idle-candidate.json", "A04-idle-candidate.json", "idle-judge-completion.json"]
    expect(listing == expected, "G1", "candidates directory holds " + ",".join(listing))
    for key, pin in PINS.items():
        expect(key in inputs, "G1", "missing input " + key)
        expect(sha256(inputs[key]) == pin, "G1", "SHA-256 differs for " + key)


def check_g2(inputs):
    receipt = parse(inputs, "cand/idle-judge-completion.json", "G2")
    candidate = parse(inputs, "cand/A03-idle-candidate.json", "G2")
    exact_keys(receipt, RECEIPT_KEYS, "G2", "receipt")
    judge = PODS["judge"]
    expect(receipt["schema_version"] == 1 and receipt["mode"] == "idle-observation-judge", "G2", "receipt schema or mode")
    expect(receipt["run_id"] == RUN_ID and receipt["source_commit"] == SOURCE, "G2", "receipt run or commit")
    expect(receipt["job_name"] == judge[0], "G2", "receipt job")
    expect(receipt["pod_name"] == judge[1] and receipt["pod_uid"] == judge[2], "G2", "receipt pod")
    expect(is_int(receipt["handler_exit_code"]) and receipt["handler_exit_code"] == 0, "G2", "receipt exit code")
    expect(receipt["cleanup_completed"] is True, "G2", "receipt cleanup")
    entries = receipt["candidates"]
    expect(isinstance(entries, list) and [e.get("scenario_id") for e in entries] == ["A02", "A03", "A04"], "G2", "receipt candidates")
    for entry in entries:
        expect(entry.get("status") == "complete_valid", "G2", "receipt entry status " + str(entry.get("scenario_id")))
        name = entry["scenario_id"] + "-idle-candidate.json"
        expect(("cand/" + name) in inputs and entry.get("candidate_sha256") == sha256(inputs["cand/" + name]), "G2", "receipt hash for " + name)
    entry = entries[1]
    expect(entry.get("candidate_name") == RUN_ID + "/idle-observation/A03/judge/candidate.json", "G2", "receipt A03 candidate name")
    assessment = candidate.get("item_assessment") if isinstance(candidate, dict) else None
    expect(isinstance(assessment, dict) and entry.get("reason") == assessment.get("reason"), "G2", "receipt A03 reason")
    return receipt


def check_g3(candidate):
    exact_keys(candidate, CANDIDATE_KEYS, "G3", "candidate")
    expect(candidate["schema_version"] == 1 and candidate["mode"] == "idle-observation-judge", "G3", "schema or mode")
    expect(candidate["synthetic"] is True and candidate["run_kind"] == "batch", "G3", "synthetic or run_kind")
    expect(candidate["run_id"] == RUN_ID and candidate["scenario_id"] == "A03", "G3", "run or scenario")
    expect(candidate["source_commit"] == SOURCE, "G3", "source commit")
    expect(candidate["managed_version"] == "2.16.0.0" and candidate["native_version"] == "2.16.0", "G3", "versions")
    expect(candidate["image_digests"] == {"loadgen": LOADGEN_DIGEST, "verifier": VERIFIER_DIGEST}, "G3", "image digests")
    expect(candidate["adr012_counts"] == "not_applicable", "G3", "adr012_counts")
    expect(candidate["notes"] == [], "G3", "notes")
    exact_keys(candidate["item_assessment"], ("verdict", "reason", "hypotheses"), "G3", "item_assessment")
    exact_keys(candidate["item_assessment"]["hypotheses"], HYPOTHESES, "G3", "hypotheses")


def check_g4(candidate):
    assessment = candidate["item_assessment"]
    expect(assessment["verdict"] == "confirmed" and assessment["reason"] == "microsoft_value_kept_connection", "G4",
           "outcome is " + str(assessment["verdict"]) + ", " + str(assessment["reason"]) + "; the report text exists only for confirmed, microsoft_value_kept_connection")


def check_produce(value, label, nullable):
    if value is None and nullable:
        return
    exact_keys(value, PRODUCE_KEYS, "G5", label)


def check_g5(candidate):
    configurations = candidate["configurations"]
    expect(isinstance(configurations, list) and [c.get("configuration_id") for c in configurations] == list(CONFIGS), "G5", "configuration order")
    for configuration in configurations:
        cid = configuration["configuration_id"]
        exact_keys(configuration, CONFIG_KEYS, "G5", cid)
        expect(configuration["status"] == "complete_valid", "G5", cid + " status")
        expect(re.fullmatch(r"[0-9a-f]{64}", str(configuration["capture_sha256"])) is not None, "G5", cid + " capture hash")
        repeats = configuration["repeats"]
        expect(isinstance(repeats, list) and [r.get("repeat") for r in repeats] == [1, 2, 3, 4, 5], "G5", cid + " repeats")
        for repeat in repeats:
            label = cid + " repeat " + str(repeat["repeat"])
            exact_keys(repeat, REPEAT_KEYS, "G5", label)
            expect(repeat["status"] == "complete_valid" and repeat["reasons"] == [], "G5", label + " status or reasons")
            expect(repeat["client_id"] == PODS[cid][1] + "-r" + str(repeat["repeat"]), "G5", label + " client_id")
            expect(repeat["connection_class"] in CLASSES, "G5", label + " class")
            check_produce(repeat["first"], label + " first", False)
            check_produce(repeat["second"], label + " second", True)
            windows = repeat["windows"]
            if repeat["connection_class"] != "first_failed":
                expect(windows is not None, "G5", label + " windows missing")
            if windows is not None:
                exact_keys(windows, WINDOW_KEYS, "G5", label + " windows")
                for key in WINDOW_KEYS:
                    value = windows[key]
                    expect(is_int(value) or (key in NULLABLE_WINDOW_KEYS and value is None), "G5", label + " windows." + key)
            expect(is_int(repeat["maximum_up_brokers"]) and repeat["maximum_up_brokers"] >= 0, "G5", label + " brokers")
            expect(isinstance(repeat["reports"], list), "G5", label + " reports")
            expect(isinstance(repeat["client_errors"], list), "G5", label + " client_errors")
            for row in repeat["client_errors"]:
                exact_keys(row, ERROR_ROW_KEYS, "G5", label + " client error row")


def classify(repeat):
    if repeat["first"]["result"] != "success":
        return "first_failed"
    windows = repeat["windows"]
    if windows["idle_timed_disconnects"] > 0:
        return "closed_while_idle"
    if windows["resume_idle_timed_disconnects"] > 0:
        return "stale_on_resume"
    return "kept"


def summarize(classes):
    if "first_failed" in classes:
        return "first_failed"
    if all(c == "kept" for c in classes):
        return "kept"
    if all(c in ("closed_while_idle", "stale_on_resume") for c in classes):
        return "not_kept"
    return "unequal"


def hypotheses(decided):
    if decided is None:
        return {name: "undecided" for name in HYPOTHESES}
    return {name: (decided if name == HYPOTHESES[0] else "recorded_only") for name in HYPOTHESES}


def item_rule(summaries):
    values = [summaries[c] for c in CONFIGS]
    if "first_failed" in values:
        return ("untestable", "first_produce_failed", hypotheses(None))
    if "unequal" in values:
        return (None, "unequal_classes", hypotheses(None))
    s0, s1 = summaries["C0"], summaries["C1"]
    if s0 == "kept" and s1 == "not_kept":
        return (None, "inverse_effect", hypotheses("undecided"))
    if s1 == "not_kept":
        return ("contradicted", "closed_with_microsoft_value", hypotheses("contradicted"))
    if s0 == "not_kept":
        return ("confirmed", "microsoft_value_kept_connection", hypotheses("holds"))
    return ("untestable", "no_idle_close_observed", hypotheses("undecided"))


def check_g6(candidate):
    summaries = {}
    for configuration in candidate["configurations"]:
        cid = configuration["configuration_id"]
        classes = []
        for repeat in configuration["repeats"]:
            expect(classify(repeat) == repeat["connection_class"], "G6", cid + " repeat " + str(repeat["repeat"]) + " class differs from recomputation")
            classes.append(repeat["connection_class"])
        expect(summarize(classes) == configuration["summary"], "G6", cid + " summary differs from recomputation")
        summaries[cid] = configuration["summary"]
    verdict, reason, statuses = item_rule(summaries)
    assessment = candidate["item_assessment"]
    expect((assessment["verdict"], assessment["reason"], assessment["hypotheses"]) == (verdict, reason, statuses), "G6", "assessment differs from recomputation")


def check_g7(inputs):
    status = parse(inputs, "pods/phase-status.json", "G7")
    batch = parse(inputs, "pods/idle-observation-batch.json", "G7")
    expect(status.get("phase") == "idle-observation" and status.get("run_id") == RUN_ID, "G7", "phase or run")
    expect(status.get("safety_stop") is False and status.get("not_selected") == [], "G7", "safety stop or not_selected")
    modes = {m.get("job"): m for m in status.get("modes", [])}
    for cid in CONFIGS:
        mode = modes.get(PODS[cid][0])
        expect(mode is not None, "G7", "no mode for " + cid)
        expect(mode.get("outcome") == "complete" and mode.get("collect") == 0, "G7", cid + " outcome or collect")
        expect(mode.get("item") == "A03" and mode.get("configuration") == cid and mode.get("role") == "loadgen", "G7", cid + " identity")
        expect(mode.get("capture_complete") is True, "G7", cid + " capture")
        expect(mode.get("capture_pod") == {"name": PODS[cid][1], "uid": PODS[cid][2]}, "G7", cid + " capture pod")
    judge = modes.get(PODS["judge"][0])
    expect(judge is not None, "G7", "no judge mode")
    expect(judge.get("mode") == "idle-observation-judge" and judge.get("outcome") == "complete" and judge.get("collect") == 0, "G7", "judge mode")
    entries = [e for e in status.get("candidates", []) if e.get("item") == "A03"]
    expect(entries == [{"item": "A03", "configuration": "judge", "accepted": True, "transport_valid": True, "candidate": "A03-idle-candidate.json", "judge_completion": "idle-judge-completion.json"}], "G7", "A03 candidate entry")
    expect(batch.get("schema_version") == 1 and batch.get("mode") == "idle-observation" and batch.get("run_kind") == "batch", "G7", "batch identity")
    expect(batch.get("selected_items") == ["A02", "A03", "A04"] and batch.get("candidates") == ["A02", "A03", "A04"], "G7", "batch items")
    expect(batch.get("run_id") == RUN_ID and batch.get("source_commit") == SOURCE, "G7", "batch run or commit")
    expect(batch.get("image_digests") == {"loadgen": LOADGEN_DIGEST, "verifier": VERIFIER_DIGEST}, "G7", "batch digests")
    expect(batch.get("evidence_passed") is True, "G7", "batch evidence_passed")


def check_g8(inputs):
    for name, (job, pod, uid, account) in PODS.items():
        collection = parse(inputs, "pods/" + job + "/collection-status.json", "G8")
        expect(collection.get("complete") is True and collection.get("gaps") == [], "G8", name + " collection")
        pods = parse(inputs, "pods/" + job + "/pods.json", "G8")
        items = pods.get("items")
        expect(isinstance(items, list) and len(items) == 1, "G8", name + " pod count")
        item = items[0]
        expect(item["metadata"]["name"] == pod and item["metadata"]["uid"] == uid, "G8", name + " pod identity")
        expect(item["spec"]["serviceAccountName"] == account, "G8", name + " service account")
        statuses = item["status"]["containerStatuses"]
        expect(len(statuses) == 1, "G8", name + " container count")
        container = statuses[0]
        digest = VERIFIER_DIGEST if name == "judge" else LOADGEN_DIGEST
        expect(container["restartCount"] == 0, "G8", name + " restarts")
        terminated = container["state"]["terminated"]
        expect(terminated["exitCode"] == 0, "G8", name + " exit code")
        expect(container["imageID"].endswith("@" + digest), "G8", name + " image digest")
        expect((terminated["startedAt"], terminated["finishedAt"]) == CONTAINER_TIMES[name], "G8", name + " container times")


def log_lines(data):
    text = data.decode("utf-8", errors="strict")
    text = re.sub(r"\x1b\[[0-9;]*m", "", text)
    return text.split("\n")


def check_g9(inputs):
    batch = log_lines(inputs["logs/batch.log"])
    stop = log_lines(inputs["logs/stop.log"])

    def at(lines, number):
        expect(len(lines) >= number, "G9", "log shorter than line " + str(number))
        return lines[number - 1]

    expect(at(batch, 940).endswith("No changes. Your infrastructure matches the configuration."), "G9", "batch line 940")
    line = at(batch, 1433)
    expect("module.eventhubs.azurerm_eventhub_namespace.this[0" in line and "will be created" in line, "G9", "batch line 1433")
    expect(at(batch, 1436).endswith("+ capacity                                  = 1"), "G9", "batch line 1436")
    expect(at(batch, 1445).endswith('+ location                                  = "uksouth"'), "G9", "batch line 1445")
    expect(at(batch, 1451).endswith('+ sku                                       = "Premium"'), "G9", "batch line 1451")
    expect(at(batch, 4027) == "2026-11-02T23:25:51.0606811Z ephemeral_state_empty", "G9", "batch line 4027")
    expect(at(stop, 343) == "2026-11-02T23:27:03.6048502Z aks_stopped", "G9", "stop line 343")


def survivor_key(row):
    return (row[0], TARGET_ABBREVIATIONS[row[1]], row[2], row[3])


def check_g10(inputs):
    summary = parse(inputs, "c5/summary.json", "G10")
    survivors = parse(inputs, "c5/survivors.json", "G10")
    expect(summary.get("commit") == MUTATION_COMMIT and summary.get("run_id") == "41484094658" and summary.get("run_attempt") == "1", "G10", "summary identity")
    expect(summary.get("measurement_complete") is True and summary.get("incomplete_reasons") == [], "G10", "measurement complete")
    expect(summary.get("break_passed") is True, "G10", "break passed")
    expect(summary.get("numerator") == COMBINED[7] and summary.get("denominator") == COMBINED[8], "G10", "summary score")
    expect(isinstance(summary.get("shards"), (dict, list)) and len(summary["shards"]) == 14, "G10", "shard count")
    targets = summary.get("targets")
    expect(isinstance(targets, dict) and list(targets) == [row[0] for row in TARGETS], "G10", "summary populations")
    for row in TARGETS:
        entry = targets[row[0]]
        expect(entry.get("target") == row[1], "G10", "target path for " + row[0])
        expect([entry["status_counts"].get(name) for name in STATUS_NAMES] == list(row[2:9]), "G10", "status counts for " + row[0])
        expect(entry.get("numerator") == row[9] and entry.get("denominator") == row[10], "G10", "score for " + row[0])
    expect([summary["status_counts"].get(name) for name in STATUS_NAMES] == list(COMBINED[:7]), "G10", "combined status counts")
    config_path = "tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json"
    expect(git_rev(config_path, MUTATION_COMMIT) == git_rev(config_path, SOURCE), "G10", "mutation configuration differs between commits")
    config = json.loads(git_show(config_path, MUTATION_COMMIT, "G10"))["stryker-config"]
    expect(config["thresholds"]["break"] == 60, "G10", "break threshold")
    expect(config["mutate"] == [row[1] for row in TARGETS], "G10", "mutate list")
    expect(is_ancestor(MUTATION_COMMIT, SOURCE), "G10", "mutation commit is not an ancestor of the run source")
    expect(is_ancestor(PRIOR_C5_COMMIT, MUTATION_COMMIT), "G10", "prior C5 commit is not an ancestor of the mutation commit")
    for row in TARGETS:
        path = "src/Parchwell.Verifier/" + row[1]
        expect(git_rev(path, MUTATION_COMMIT) == git_rev(path, SOURCE), "G10", "blob differs at the run source for " + row[1])
        if row[0] != "idle-observation":
            expect(git_rev(path, PRIOR_C5_COMMIT) == git_rev(path, MUTATION_COMMIT), "G10", "blob differs at the prior commit for " + row[1])
    exact_keys(survivors, ("survivors", "disposition_sample", "unreviewed_beyond_cap"), "G10", "survivors.json")
    expect(len(survivors["survivors"]) == 38 and survivors["disposition_sample"] == survivors["survivors"], "G10", "survivor sample")
    expect(survivors["unreviewed_beyond_cap"] == 0, "G10", "unreviewed_beyond_cap")
    found = set()
    for item in survivors["survivors"]:
        expect(item.get("status") == "Survived", "G10", "survivor status")
        found.add((int(item["id"]), item["file"], item["location"]["start"]["line"], item["mutatorName"]))
    expect(found == {survivor_key(row) for row in SURVIVORS}, "G10", "survivor set differs from the recorded dispositions")
    row = next(item for item in survivors["survivors"] if int(item["id"]) == 13251)
    source = git_show("src/Parchwell.Verifier/" + row["file"], MUTATION_COMMIT, "G10").decode("utf-8").split("\n")
    line = source[row["location"]["start"]["line"] - 1]
    expect(line[row["location"]["start"]["column"] - 1:row["location"]["end"]["column"] - 1] == '"incomplete"', "G10", "survivor 13251 source span")
    return summary


def check_g11(sources, expected):
    for path, pin in SOURCES.items():
        committed = git_show(path)
        expect(sha256(committed) == pin, "G11", "committed " + path)
        if path in WORKING_TREE_SOURCES:
            expect(sources[path] == committed, "G11", "working tree differs for " + path)
    data = expected
    expect(sha256(data) == SOURCES["scenarios/A03/expected.yaml"], "G11", "expected.yaml bytes differ from the pin")
    for name, text, wanted in (("expected.yaml", data, EXPECTED_LINES),
                               ("scenario.yaml", sources["scenarios/A03/scenario.yaml"], SCENARIO_LINES),
                               ("BaselineSettingsCatalog.cs", git_show("src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs"), BASELINE_LINES)):
        lines = text.decode("utf-8").split("\n")
        for line in wanted:
            expect(lines.count(line) == 1, "G11", name + " line missing or repeated: " + line[:60])


def quiet_ms(broker):
    if broker is None:
        return 0
    tx, rx = broker["tx_idle_us"], broker["rx_idle_us"]
    if tx == -1 and rx == -1:
        return 0
    micros = rx if tx == -1 else tx if rx == -1 else min(tx, rx)
    return micros // 1000


def check_g12(candidate):
    maxima = {}
    disconnects = []
    steps = []
    for configuration in candidate["configurations"]:
        cid = configuration["configuration_id"]
        maxima[cid] = []
        for repeat in configuration["repeats"]:
            reports = repeat["reports"]
            maxima[cid].append(max([quiet_ms(b) for report in reports for b in report["brokers"].values()] or [0]))
            for index in range(1, len(reports)):
                before = reports[index - 1]["brokers"]
                for key, broker in reports[index]["brokers"].items():
                    earlier = before.get(key)
                    rose = broker["disconnects"] - (earlier["disconnects"] if earlier else 0)
                    if rose > 0:
                        timeouts = broker["req_timeouts"] - (earlier["req_timeouts"] if earlier else 0)
                        disconnects.append((cid, repeat["repeat"], reports[index]["sequence"], rose, quiet_ms(earlier), timeouts))
                    added = broker["metadata"] - (earlier["metadata"] if earlier else 0)
                    if added > 0:
                        steps.append((cid, repeat["repeat"], reports[index]["sequence"], reports[index]["observed_elapsed_ms"], added))
            expect(repeat["windows"]["idle_metadata_requests"] == IDLE_METADATA[cid], "G12", cid + " repeat " + str(repeat["repeat"]) + " idle Metadata requests")
    expect(maxima == REPEAT_MAX_QUIET, "G12", "largest quiet periods differ")
    expect(disconnects == DISCONNECTS, "G12", "disconnect rows differ")
    expect(steps == METADATA_STEPS, "G12", "Metadata request rows differ")


def read_sources():
    return {path: (ROOT / path).read_bytes() for path in WORKING_TREE_SOURCES}


def verify(inputs, pins=True, sources=None):
    if pins:
        check_g1(inputs)
    receipt = check_g2(inputs)
    candidate = parse(inputs, "cand/A03-idle-candidate.json", "G3")
    check_g3(candidate)
    check_g4(candidate)
    check_g5(candidate)
    check_g6(candidate)
    check_g7(inputs)
    check_g8(inputs)
    check_g9(inputs)
    summary = check_g10(inputs)
    sources = sources if sources is not None else read_sources()
    check_g11(sources, sources["scenarios/A03/expected.yaml"])
    check_g12(candidate)
    return {"candidate": candidate, "receipt": receipt, "summary": summary, "inputs": inputs}


def survivor_rows():
    return sorted((m, TARGET_ABBREVIATIONS[t], line, mutator, disposition, proof, section)
                  for m, t, line, mutator, disposition, proof, section in SURVIVORS)


def job_rows():
    rows = []
    for name in CONFIGS + ("judge",):
        job, pod, uid, account = PODS[name]
        container = "verifier" if name == "judge" else "loadgen"
        rows.append({
            "configuration": "all" if name == "judge" else name,
            "stage": "judge" if name == "judge" else "idle",
            "job": job, "account": account, "pod": pod, "pod_uid": uid,
            "container_started": CONTAINER_TIMES[name][0], "container_finished": CONTAINER_TIMES[name][1],
            "pod_log_member": job + "/" + pod + ".containers." + container + ".log",
            "pod_json_member": job + "/pods.json",
        })
    return rows


def render_json(ctx):
    candidate = ctx["candidate"]
    assessment = candidate["item_assessment"]
    survivors = [{"id": m, "target": t, "line": line, "mutator": mutator, "recorded_disposition": disposition,
                  "proof": proof, "recorded_in": SECTIONS[section][1]}
                 for m, t, line, mutator, disposition, proof, section in survivor_rows()]
    document = {
        "publication": {
            "item": "A03", "proposed_verdict": assessment["verdict"], "reason": assessment["reason"],
            "hypotheses": assessment["hypotheses"],
            "verdict_authority": "issue 301 decision 6563540913 point 1, adopted on issue 321 by decision 6563546186; run choice: issue 321 decision 6596020684",
            "workload": "synthetic", "adr012_counts": "not_applicable", "notes": [],
            "metadata_settings_ms": {c: {"metadata.max.age.ms": int(TIMERS[c][0]), "topic.metadata.refresh.interval.ms": int(TIMERS[c][1])} for c in CONFIGS},
            "finding": {
                "expected": "not_kept in C1 as in C0, an inference from rdkafka.c L2458-L2461",
                "observed_summaries": {c["configuration_id"]: c["summary"] for c in candidate["configurations"]},
                "repeat_max_quiet_ms": REPEAT_MAX_QUIET,
                "disconnects": [{"configuration": r[0], "repeat": r[1], "report": r[2], "disconnects_added": r[3],
                                 "quiet_before_ms": r[4], "request_timeouts_added": r[5]} for r in DISCONNECTS],
                "metadata_requests": [{"configuration": r[0], "repeat": r[1], "report": r[2], "report_elapsed_ms": r[3],
                                       "requests_added": r[4]} for r in METADATA_STEPS],
            },
            "run": {
                "workflow_run": RUN, "attempt": 1, "run_id": RUN_ID, "source_commit": SOURCE, "run_kind": "batch",
                "conclusion": "success", "created_at": "2026-11-02T22:07:00Z",
                "candidate_artifact": {"id": 13187681970, "name": "idle-observation-candidates-41534423570-1", "zip_sha256": "9e2376e4fb39f2491f931b217d574db2ad325f52e457f5709d330be82ed186ec"},
                "pod_artifact": {"id": 13188401369, "name": "idle-observation-pods-41534423570-1", "zip_sha256": "35a992329edcd87dfd1d84f636228f1ff417791dd8626c4df823db597404df42"},
                "artifacts_expire_at": "2027-01-31T22:07:02Z", "batch_job_id": 125582551893, "stop_job_id": 125619643005,
                "batch_log_sha256": PINS["logs/batch.log"], "stop_log_sha256": PINS["logs/stop.log"],
            },
            "rules": {
                "scenario_yaml_sha256": SOURCES["scenarios/A03/scenario.yaml"],
                "expected_yaml_sha256": SOURCES["scenarios/A03/expected.yaml"],
                "plan": "issue 319 comments 6575747094 and 6579253743, Revision 6",
                "plan_check": "issue 319 comment 6579727682", "pull_request": 328,
                "merged_at": "2026-11-02T16:39:43Z", "merge_commit": MUTATION_COMMIT,
                "run_source_pull_request": 330, "run_source_merged_at": "2026-11-02T22:06:44Z", "run_source_commit": SOURCE,
            },
            "environment": {
                "region": "uksouth", "event_hubs_sku": "Premium", "pu_count": 1, "node_size": "Standard_D4ds_v5",
                "node_size_evidence": "inferred_from_pinned_configuration_and_no_change_cluster_plan",
                "managed_version": "2.16.0.0", "native_version": "2.16.0",
                "image_digests": {"loadgen": LOADGEN_DIGEST, "verifier": VERIFIER_DIGEST},
                "ephemeral_state_empty_at": "2026-11-02T23:25:51.0606811Z", "aks_stopped_at": "2026-11-02T23:27:03.6048502Z",
            },
            "jobs": job_rows(),
            "mutation_gate": {
                "workflow_run": C5_RUN, "attempt": 1,
                "summary_artifact": {"id": 13170506349, "name": "c5-summary-41484094658-1", "zip_sha256": "91121e3d5b09a69b230e148e13336b61da0434d20840e33e87262264a9086c40"},
                "summary_json_sha256": PINS["c5/summary.json"], "survivors_json_sha256": PINS["c5/survivors.json"],
                "measured_commit": MUTATION_COMMIT, "targets_identical_at": SOURCE,
                "measurement_complete": True, "break_passed": True, "break_threshold": 60,
                "numerator": 3752, "denominator": 3790, "survivors": survivors,
            },
        },
        "judge_receipt": ctx["receipt"],
        "candidate": candidate,
    }
    return (json.dumps(document, indent=2, ensure_ascii=True) + "\n").encode()


def cell(value, ticks=False):
    if value is None:
        return "none"
    return "`" + str(value) + "`" if ticks else str(value)


def row(cells):
    return "| " + " | ".join(cells) + " |"


def class_counts(configuration):
    counts = [(name, sum(1 for r in configuration["repeats"] if r["connection_class"] == name)) for name in CLASSES]
    return ", ".join(str(count) + " `" + name + "`" for name, count in counts if count > 0)


def table_block(name, ctx):
    candidate = ctx["candidate"]
    configurations = candidate["configurations"]
    lines = []
    if name == "configurations":
        for c in configurations:
            cid = c["configuration_id"]
            lines.append(row([cid, TIMERS[cid][0], TIMERS[cid][1], cell(c["status"], True), cell(c["summary"], True), cell(c["capture_sha256"], True)]))
    elif name == "repeat-outcomes":
        for c in configurations:
            for r in c["repeats"]:
                second = r["second"]
                lines.append(row([
                    c["configuration_id"], str(r["repeat"]), cell(r["status"], True), cell(r["connection_class"], True),
                    cell(r["first"]["result"], True), cell(r["first"]["error_code"]), cell(r["first"]["latency_ms"]),
                    cell(second["result"] if second else None, True), cell(second["error_code"] if second else None),
                    cell(second["latency_ms"] if second else None), str(r["maximum_up_brokers"]),
                ]))
    elif name == "windows":
        for c in configurations:
            for r in c["repeats"]:
                w = r["windows"] or {}
                lines.append(row([c["configuration_id"], str(r["repeat"])] + [cell(w.get(k)) for k in WINDOW_KEYS] + [str(len(r["client_errors"])), str(r["client_errors_dropped"])]))
    elif name == "client-errors":
        for c in configurations:
            for r in c["repeats"]:
                for e in r["client_errors"]:
                    lines.append(row([c["configuration_id"], str(r["repeat"]), cell(e["code"]), "yes" if e["is_fatal"] else "no", cell(e["origin"], True), cell(e["observed_elapsed_ms"])]))
        if not lines:
            return ["No client error callback was captured in any repeat."]
    elif name == "hypotheses":
        for key in HYPOTHESES:
            lines.append(row([key, cell(candidate["item_assessment"]["hypotheses"][key], True)]))
    elif name == "disconnects":
        for r in DISCONNECTS:
            lines.append(row([r[0], str(r[1]), str(REPEAT_MAX_QUIET[r[0]][r[1] - 1]), str(r[2]), str(r[4]), str(r[5])]))
    elif name == "metadata-requests":
        for r in METADATA_STEPS:
            lines.append(row([r[0], str(r[1]), str(r[2]), str(r[3]), str(r[4])]))
    elif name == "jobs":
        for j in job_rows():
            lines.append(row([j["configuration"], j["stage"], "`" + j["job"] + "` (`" + j["account"] + "`)", j["container_started"],
                              j["container_finished"], "`" + j["pod_log_member"] + "`", "`" + j["pod_json_member"] + "`"]))
    elif name == "mutation-targets":
        for t in TARGETS:
            lines.append(row(["`" + t[1] + "`"] + [str(v) for v in t[2:9]] + [str(t[9]) + " / " + str(t[10])]))
        lines.append(row(["All seven targets"] + [str(v) for v in COMBINED[:7]] + [str(COMBINED[7]) + " / " + str(COMBINED[8])]))
    elif name == "survivors":
        for mutant, target, line, mutator, disposition, proof, section in survivor_rows():
            label, anchor = SECTIONS[section]
            lines.append(row([str(mutant), "`" + target + "`", str(line), mutator, disposition, proof, "[" + label + "](" + MUTATION_DOC + "#" + anchor + ")"]))
    return TABLE_HEADERS[name] + lines


def meta_reports(cid):
    sequences = sorted({r[2] for r in METADATA_STEPS if r[0] == cid})
    return " and ".join(str(s) for s in sequences)


def tokens(ctx):
    by_id = {c["configuration_id"]: c for c in ctx["candidate"]["configurations"]}
    closes = [r[4] for r in DISCONNECTS if r[5] == 0]
    values = {
        "C0_CLASSES": class_counts(by_id["C0"]), "C1_CLASSES": class_counts(by_id["C1"]), "C2_CLASSES": class_counts(by_id["C2"]),
        "C2_SUMMARY": by_id["C2"]["summary"], "DECISION": DECISION, "PRECEDENCE": PRECEDENCE,
        "CLOSE_MIN": str(min(closes)), "CLOSE_MAX": str(max(closes)),
    }
    for cid in CONFIGS:
        values["MAX_" + cid + "_MIN"] = str(min(REPEAT_MAX_QUIET[cid]))
        values["MAX_" + cid + "_MAX"] = str(max(REPEAT_MAX_QUIET[cid]))
        values[cid + "_META_REPORTS"] = meta_reports(cid)
    return values


def fill(text, values):
    for key, value in values.items():
        text = text.replace("@@" + key + "@@", value)
    return text


def readme_row(ctx):
    return "| A03 | Metadata refresh interval | " + fill(OBSERVED, tokens(ctx)) + " | confirmed | [Report](results/A03/2026-11-02.md) |"


def render_md(ctx):
    out = []
    for line in TEMPLATE.split("\n"):
        match = re.fullmatch(r"<!-- tables:([a-z-]+) -->", line)
        out.append(line)
        if match:
            out.extend(table_block(match.group(1), ctx))
            out.append("<!-- /tables:" + match.group(1) + " -->")
    text = fill("\n".join(out), tokens(ctx))
    if "@@" in text:
        fail("G13", "unreplaced token in the report")
    return (text + "\n").encode("ascii")


def check_output(ctx):
    for name, data in (("2026-11-02.json", render_json(ctx)), ("2026-11-02.md", render_md(ctx))):
        path = ROOT / "results" / "A03" / name
        expect(path.is_file() and path.read_bytes() == data, "G13", "results/A03/" + name + " differs from the rendered bytes")
    lines = (ROOT / "README.md").read_text(encoding="utf-8").split("\n")
    expect(len(lines) > 14 and lines[14] == readme_row(ctx), "G13", "README.md line 15 differs")


def edit_json(inputs, key, change):
    value = strict_json(inputs[key])
    change(value)
    inputs[key] = json.dumps(value, separators=(",", ":")).encode()


def refresh_receipt(inputs):
    def change(receipt):
        receipt["candidates"][1]["candidate_sha256"] = sha256(inputs["cand/A03-idle-candidate.json"])
        receipt["candidates"][1]["reason"] = strict_json(inputs["cand/A03-idle-candidate.json"])["item_assessment"]["reason"]
    edit_json(inputs, "cand/idle-judge-completion.json", change)


def candidate_edit(change):
    return lambda i: edit_json(i, "cand/A03-idle-candidate.json", change)


def self_test(inputs, sources):
    verify(inputs, pins=True, sources=sources)
    cases = []

    def case(name, code, mutate, pins=False, refresh=False):
        cases.append((name, code, mutate, pins, refresh))

    def append_newline(i):
        i["cand/A03-idle-candidate.json"] += b"\n"

    def add_extra(i):
        i["cand-listing"] = json.dumps(sorted(json.loads(i["cand-listing"]) + ["extra.json"])).encode()

    def break_hash(receipt):
        entry = receipt["candidates"][1]
        entry["candidate_sha256"] = entry["candidate_sha256"][:-1] + ("0" if entry["candidate_sha256"][-1] != "0" else "1")

    def outcome(c):
        c["item_assessment"].update(verdict="contradicted", reason="closed_with_microsoft_value")

    def flip_class(c):
        repeat = c["configurations"][0]["repeats"][1]
        repeat["connection_class"] = "kept"

    def set_quiet(c):
        broker = next(iter(c["configurations"][2]["repeats"][0]["reports"][0]["brokers"].values()))
        broker["tx_idle_us"] = broker["rx_idle_us"] = 999999999

    def drop_metadata(c):
        reports = c["configurations"][1]["repeats"][0]["reports"]
        for key, broker in reports[12]["brokers"].items():
            broker["metadata"] = reports[11]["brokers"][key]["metadata"] if key in reports[11]["brokers"] else 0

    def window_metadata(c):
        c["configurations"][2]["repeats"][4]["windows"]["idle_metadata_requests"] = 1

    def restart(i):
        edit_json(i, "pods/" + PODS["C2"][0] + "/pods.json", lambda p: p["items"][0]["status"]["containerStatuses"][0].update(restartCount=1))

    def gap(i):
        edit_json(i, "pods/" + PODS["judge"][0] + "/collection-status.json", lambda s: s.update(gaps=["x"]))

    def teardown(i):
        lines = i["logs/batch.log"].decode("utf-8").split("\n")
        lines[4026] = "x"
        i["logs/batch.log"] = "\n".join(lines).encode()

    def drop_survivor(s):
        s["survivors"].pop()
        s["disposition_sample"].pop()

    case("G1-append", "G1", append_newline, pins=True)
    case("G1-extra", "G1", add_extra, pins=True)
    case("G2-exit", "G2", lambda i: edit_json(i, "cand/idle-judge-completion.json", lambda r: r.update(handler_exit_code=1)))
    case("G2-hash", "G2", lambda i: edit_json(i, "cand/idle-judge-completion.json", break_hash))
    case("G3-notes", "G3", candidate_edit(lambda c: c.update(notes=["k37_not_tested"])), refresh=True)
    case("G3-kind", "G3", candidate_edit(lambda c: c.update(run_kind="smoke")), refresh=True)
    case("G4-outcome", "G4", candidate_edit(outcome), refresh=True)
    case("G5-status", "G5", candidate_edit(lambda c: c["configurations"][2]["repeats"][2].update(status="incomplete")), refresh=True)
    case("G5-client", "G5", candidate_edit(lambda c: c["configurations"][0]["repeats"][0].update(client_id="x-r1")), refresh=True)
    case("G6-class", "G6", candidate_edit(flip_class), refresh=True)
    case("G6-hypothesis", "G6", candidate_edit(lambda c: c["item_assessment"]["hypotheses"].update(H_refresh_interval="holds")), refresh=True)
    case("G6-c2-summary", "G6", candidate_edit(lambda c: c["configurations"][2].update(summary="not_kept")), refresh=True)
    case("G7-stop", "G7", lambda i: edit_json(i, "pods/phase-status.json", lambda s: s.update(safety_stop=True)))
    case("G7-evidence", "G7", lambda i: edit_json(i, "pods/idle-observation-batch.json", lambda b: b.update(evidence_passed=False)))
    case("G8-restart", "G8", restart)
    case("G8-gap", "G8", gap)
    case("G9-teardown", "G9", teardown)
    case("G10-break", "G10", lambda i: edit_json(i, "c5/summary.json", lambda s: s.update(break_passed=False)))
    case("G10-commit", "G10", lambda i: edit_json(i, "c5/summary.json", lambda s: s.update(commit=SOURCE)))
    case("G10-survivor", "G10", lambda i: edit_json(i, "c5/survivors.json", drop_survivor))
    case("G12-max", "G12", candidate_edit(set_quiet), refresh=True)
    case("G12-metadata", "G12", candidate_edit(drop_metadata), refresh=True)
    case("G12-window", "G12", candidate_edit(window_metadata), refresh=True)

    for name, code, mutate, pins, refresh in cases:
        copy = dict(inputs)
        mutate(copy)
        if refresh:
            refresh_receipt(copy)
        try:
            verify(copy, pins=pins, sources=sources)
            print("FAIL " + name + ": no check failed", file=sys.stderr)
            return 1
        except CheckFailure as error:
            if error.code != code:
                print("FAIL " + name + ": expected " + code + ", got " + error.code + ": " + error.message, file=sys.stderr)
                return 1
        print("PASS " + name)
    for name, change in (("G11-expected", lambda s: check_g11(s, s["scenarios/A03/expected.yaml"] + b"x")),
                         ("G11-scenario", lambda s: check_g11(dict(s, **{"scenarios/A03/scenario.yaml": s["scenarios/A03/scenario.yaml"] + b"x"}), s["scenarios/A03/expected.yaml"]))):
        try:
            change(sources)
            print("FAIL " + name + ": no check failed", file=sys.stderr)
            return 1
        except CheckFailure as error:
            if error.code != "G11":
                print("FAIL " + name + ": expected G11, got " + error.code, file=sys.stderr)
                return 1
        print("PASS " + name)
    return 0


def main(argv):
    parser = argparse.ArgumentParser(description="Assemble the A03 publication offline.")
    parser.add_argument("--candidates", required=True)
    parser.add_argument("--pods", required=True)
    parser.add_argument("--c5", required=True)
    parser.add_argument("--batch-log", required=True)
    parser.add_argument("--stop-log", required=True)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--readme-row", action="store_true")
    mode.add_argument("--self-test", action="store_true")
    args = parser.parse_args(argv)
    try:
        inputs = load(args)
        if args.self_test:
            return self_test(inputs, read_sources())
        ctx = verify(inputs)
        if args.readme_row:
            print(readme_row(ctx))
            return 0
        json_bytes = render_json(ctx)
        md_bytes = render_md(ctx)
        if args.check:
            check_output(ctx)
            return 0
        out = ROOT / "results" / "A03"
        out.mkdir(parents=True, exist_ok=True)
        (out / "2026-11-02.json").write_bytes(json_bytes)
        (out / "2026-11-02.md").write_bytes(md_bytes)
        return 0
    except CheckFailure as error:
        print("FAIL " + error.code + ": " + error.message, file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

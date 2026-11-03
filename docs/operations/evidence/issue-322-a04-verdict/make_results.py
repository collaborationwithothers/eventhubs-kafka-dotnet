"""Assemble the A04 publication from batch run 41602168787, offline.

Purpose: build results/A04/2026-11-03.json and results/A04/2026-11-03.md, the
candidate-derived result and report for the A04 verdict pull request (issue 322),
from the idle-observation batch run 41602168787 and the mutation run
41602164797. Nothing here reaches Azure or GitHub.

Command line:

    python3 -I docs/operations/evidence/issue-322-a04-verdict/make_results.py \
        --candidates DIR --pods DIR --c5 DIR --batch-log FILE --stop-log FILE \
        [--check | --readme-row | --self-test]

Supported inputs: the extracted candidate and pod artifacts of attempt 1, the
extracted C5 summary artifact of run 41602164797, the two job logs, and this
checkout's git objects for c878a9a and f0850d4.

Output: with no mode flag it writes only results/A04/2026-11-03.json and
results/A04/2026-11-03.md. It is not a second scientific validator. The
verifier's candidate stays authoritative; the transcribed rules only confirm it.

Failure conditions (exit 1, "FAIL G<n>: <message>" on stderr):
  G1  an input is missing, extra, or does not match its pinned SHA-256
  G2  the judge receipt does not match the run, pod, or candidate
  G3  the candidate identity, versions, digests, notes or keys differ
  G4  the outcome is not `undocumented`, `not_in_librdkafka_table` with H_client_close `holds`
  G5  a configuration or repeat has the wrong shape, status or client ID
  G6  a recomputed class, summary, verdict, reason, hypothesis or prior-rule outcome differs
  G7  the phase status or batch record differs
  G8  a pod, container, exit code, image digest or time differs
  G9  a pinned job-log line differs
  G10 the mutation summary, survivor counts or measured sources differ
  G11 a source file or a pinned source line differs
  G12 a recomputed quiet maximum, disconnect, Metadata step, first close or broker host differs
  G13 (--check only) a generated file or README line 16 differs

Regression example: copy the candidates directory, append a newline to
A04-idle-candidate.json and run with --check: G1 fails before anything is written.
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
RUN = REPO + "/actions/runs/41602168787"
RUN_ID = "idle-41602168787-1"
SOURCE = "c878a9ab2b2550e17ee4cbb5a6388f96f93d6c62"
C5_RUN = REPO + "/actions/runs/41602164797"
PRIOR_C5_COMMIT = "f0850d44086c4065cad76e64da6ec6f2d9cc50ce"
LOADGEN_DIGEST = "sha256:c43ca9fc2c5ac759cef722f0fe0506058d3064d6e21b88749dbad9c223350750"
VERIFIER_DIGEST = "sha256:971906cace285b307da14a97386a158d425b09035aab14c0052e6d6adcbb3f27"
HOST = "eh-parchwell-a01prod1.servicebus.windows.net"
CONFIGS = ("C0", "C1")
PODS = {
    "C0": ("idle-41602168787-1-a04-c0-idle", "idle-41602168787-1-a04-c0-idle-zd9nc", "37d4ce1e-d1ae-4b37-8718-ac01b8a1a5cd", "loadgen"),
    "C1": ("idle-41602168787-1-a04-c1-idle", "idle-41602168787-1-a04-c1-idle-f6rcv", "70859c0e-d895-4edd-8cc1-5bcbe472425d", "loadgen"),
    "judge": ("idle-41602168787-1-idle-judge", "idle-41602168787-1-idle-judge-sr4jp", "bb945a74-3ae8-48bb-9271-8ed0808fb185", "verifier"),
}
# connections.max.idle.ms, metadata.max.age.ms, topic.metadata.refresh.interval.ms
SETTINGS = {"C0": ("omitted", "900000", "900000"), "C1": ("180000", "900000", "900000")}
CONTAINER_TIMES = {
    "C0": ("2026-11-03T08:53:52Z", "2026-11-03T09:02:14Z"),
    "C1": ("2026-11-03T09:02:26Z", "2026-11-03T09:09:50Z"),
    "judge": ("2026-11-03T09:10:07Z", "2026-11-03T09:10:10Z"),
}
REPEAT_MAX_QUIET = {
    "C0": [284726, 404650, 419829, 284805, 284552],
    "C1": [165485, 178798, 165575, 179774, 165586],
}
# (configuration, repeat, report sequence, report observed_elapsed_ms, broker node_id,
#  disconnects added, quiet ms in the previous report, req_timeouts added, connects added)
DISCONNECTS = [
    ("C0", 1, 20, 300072, 8, 1, 284726, 0, 0),
    ("C0", 2, 28, 420024, 8, 1, 404650, 0, 0),
    ("C0", 3, 33, 495026, 8, 1, 59744, 1, 1),
    ("C0", 4, 20, 300018, 8, 1, 284805, 0, 0),
    ("C0", 5, 20, 300018, 8, 1, 284552, 0, 0),
    ("C1", 1, 12, 180069, 8, 1, 164797, 0, 0), ("C1", 1, 24, 360077, 12, 1, 165485, 0, 0),
    ("C1", 2, 13, 195015, 8, 1, 178798, 0, 0), ("C1", 2, 25, 375023, 13, 1, 178614, 0, 0),
    ("C1", 3, 12, 180016, 8, 1, 164753, 0, 0), ("C1", 3, 24, 360024, 12, 1, 165575, 0, 0),
    ("C1", 4, 13, 195015, 8, 1, 179774, 0, 0), ("C1", 4, 25, 375022, 6, 1, 179604, 0, 0),
    ("C1", 5, 12, 180013, 8, 1, 164789, 0, 0), ("C1", 5, 24, 360021, 5, 1, 165586, 0, 0),
]
METADATA_STEPS = []
PRIOR_RULE = (None, "unequal_classes")

PINS = {
    "cand/A04-idle-candidate.json": "c8342147d98d9636c7e3fa3438c8c1c921e0ac33758ad2552e0a3c9db69ea023",
    "cand/idle-judge-completion.json": "261851eb60b505014e4efc771e9fc5db255204256eb261ec55b5824ce6a5fa53",
    "pods/phase-status.json": "ad9c0deb8d910ca9d509dd7bb547db0211aaf7f17af602377fd6a60020b822a7",
    "pods/idle-observation-batch.json": "87aa7db27b96a88fe4b53423b07c219293770a4681390d32b0fe9a1f334640d7",
    "pods/idle-41602168787-1-a04-c0-idle/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "pods/idle-41602168787-1-a04-c0-idle/idle-41602168787-1-a04-c0-idle-zd9nc.containers.loadgen.log": "0529c779205d81b17dcf616eb5094b529745f1ab04ad0418525634a496506c56",
    "pods/idle-41602168787-1-a04-c0-idle/pods.json": "bfeb065bc6a12804ff0c5ad1e24ed68c68795ef7681dce0edd1c7801acfde4af",
    "pods/idle-41602168787-1-a04-c1-idle/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "pods/idle-41602168787-1-a04-c1-idle/idle-41602168787-1-a04-c1-idle-f6rcv.containers.loadgen.log": "14c441eb73ab33bdc124515de952a49dcaf387807885873ef46441b20a5e91c6",
    "pods/idle-41602168787-1-a04-c1-idle/pods.json": "0e59d72c1129f3897080865b5d5127ea6adc5bb98c3fafabee22c776bd3f6055",
    "pods/idle-41602168787-1-idle-judge/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "pods/idle-41602168787-1-idle-judge/idle-41602168787-1-idle-judge-sr4jp.containers.verifier.log": "197871f001e6cea52c11df077e495c9b5f1d8e8997a25206c79cc617d4226326",
    "pods/idle-41602168787-1-idle-judge/pods.json": "c3122871c794ea4c1b4cde8021d206c91d7c281220d715a949d4293c11e28b3c",
    "c5/summary.json": "0cedd09cb1a61ebd16abfc9ee47a4720baa4abdc1639a3eecb3bc3f8937d545e",
    "c5/summary.md": "61006a347fc867c134e08389af06433dc2bfa21d0ef0cfb6c955436a9c732bcc",
    "c5/survivors.json": "a1b5c1d9c7aaab95e275b683770d1d22d97e0196ddcac08a43415bc19003a4fe",
    "logs/batch.log": "a747f60c3568fa287f91869c38dec5dc1706679b1dcb653b2c699f9b10b95632",
    "logs/stop.log": "76bd6dde11f5d8f866276bfdd3ec15d964d1e1dcd9d690b0660f0a71545fafe6",
}

SOURCES = {
    "scenarios/A04/scenario.yaml": "ffc06a71e84d2af3f1b27242643cec5b1e9a953f2b17e54b826d79ca0bdc4abd",
    "scenarios/A04/expected.yaml": "ca56b9be91ed004982b4e33925e04308aebfa2228939a963466dfc41b91fa291",
    "infra/cluster/aks.tf": "969762e923eae813525207d50f5689de9fbed20c0c4c2c274367bcb54462a8a2",
    "infra/ephemeral/variables.tf": "eefef680580b236a28505077e85804960d2d6cac67fe6495bd3bb5b34dbb5307",
    "src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs": "a1395a1ab548ec97c42a04af73051987da114bc921d08babc2a2d6a91fe51420",
}
WORKING_TREE_SOURCES = ("scenarios/A04/scenario.yaml", "scenarios/A04/expected.yaml")

# (population, target, Killed, Timeout, Survived, NoCoverage, CompileError, RuntimeError, Ignored, numerator, denominator)
TARGETS = [
    ("phase", "Judge/PhaseJudge.cs", 189, 1, 16, 0, 4, 0, 6, 190, 206),
    ("publication", "VerifyRun/Publication/PublicationAcceptance.cs", 177, 0, 6, 0, 6, 0, 14, 177, 183),
    ("a01", "VerifyRun/A01/A01EvidenceRecorder.cs", 71, 0, 0, 0, 15, 0, 8, 71, 71),
    ("workload", "VerifyRun/A01/WorkloadRecorder.cs", 95, 0, 0, 0, 2, 0, 7, 95, 95),
    ("single-action-repeat", "VerifyRun/SingleAction/SingleActionRepeatJudgement.cs", 792, 0, 8, 0, 142, 0, 29, 792, 800),
    ("single-action-configuration", "VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs", 1966, 0, 45, 0, 158, 0, 46, 1966, 2011),
    ("idle-observation", "VerifyRun/IdleObservation/IdleObservationJudgement.cs", 433, 0, 14, 0, 101, 0, 15, 433, 447),
]
STATUS_NAMES = ("Killed", "Timeout", "Survived", "NoCoverage", "CompileError", "RuntimeError", "Ignored")
COMBINED = (3723, 1, 89, 0, 428, 0, 125, 3724, 3813)
SHARD_COUNT = 18
SAMPLE_SIZE = 50
UNREVIEWED = 39
PRIOR_SAC_SURVIVORS = 8
SAC = "VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs"

DECISION = "holds when every C1 repeat's first idle-window disconnect has `first_close_quiet_ms >= 163000` and `first_close_after_idle_ms < 225000`; else, including a repeat with no idle-window disconnect, contradicted. `H_default_close` (C0): recorded only. Verdict: undocumented, `not_in_librdkafka_table`."
PRECEDENCE = "smoke run -> null, `smoke_only`; a missing candidate or any configuration not `complete_valid` -> null, `incomplete_configuration`; any repeat `first_failed` -> `untestable`, `first_produce_failed`; any configuration whose five repeats do not share one survival -> null, `unequal_classes`; then the item rule."
EXPECTED_LINES = [
    '    decision: "' + DECISION + '"',
    '  decision: "' + DECISION + '"',
    '  precedence: "' + PRECEDENCE + '"',
    '    decision: "recorded_only; the heuristic cannot fire because namespace names exclude \\"azure\\" (K-36; infra/ephemeral/variables.tf:33)"',
    '  connection_classes: [kept, closed_while_idle, stale_on_resume, timed_out_on_resume, first_failed]',
    '  idle_timed_threshold_ms: 225000',
    '    limit_ms: 225000',
    '    quiet_floor_ms: 163000',
    '  rule_history: "timed_out_on_resume was designed after batch run 41534423570 was seen (decision 6596426514) and applies only to runs at a source commit that contains it; run 41534423570 was judged at 9014b8effd4f8556d3446bcfbf8f951a52905bb2, without it"',
    '  text_must_state: "the 225000 ms idle_timed threshold is a harness choice, not a measurement; per repeat, the other_cause disconnects, request timeouts, second produce outcome, client error callbacks and idle_metadata_requests. The 163000 ms quiet floor is a harness choice, not a measurement. Both metadata timers were 900000 ms in C0 and C1, not the baseline\'s 180000 ms metadata.max.age.ms and 300000 ms topic.metadata.refresh.interval.ms. The timed_out_on_resume class was designed after batch run 41534423570 was seen and is applied only to a later run."',
]
SCENARIO_LINES = {
    "      connections.max.idle.ms: omit": 1,
    "      connections.max.idle.ms: 180000": 1,
    "      metadata.max.age.ms: 900000": 2,
    "      topic.metadata.refresh.interval.ms: 900000": 2,
    "  idle_seconds: 420": 1,
}
BASELINE_LINES = [
    '        KafkaSettingDefinition.Explicit(KafkaSettingKeys.SocketKeepaliveEnable, "true", ["A02"], [MicrosoftGuidance],',
    '        KafkaSettingDefinition.Explicit(KafkaSettingKeys.ConnectionsMaxIdleMs, "0", ["A04"], [NativeConfiguration, Blueprint],',
    '        KafkaSettingDefinition.Explicit(KafkaSettingKeys.RequestTimeoutMs, "60000", ["A01"], [MicrosoftGuidance, NativeConfiguration, Blueprint, ApprovedPlan],',
]
VARIABLES_LINE = '    condition     = try(can(regex("^[a-zA-Z][a-zA-Z0-9-]{4,48}[a-zA-Z0-9]$", var.eventhub_namespace_name)) && !strcontains(lower(var.eventhub_namespace_name), "azure"), false)'

CLASSES = ("kept", "closed_while_idle", "stale_on_resume", "timed_out_on_resume", "first_failed")
NOT_KEPT = ("closed_while_idle", "stale_on_resume", "timed_out_on_resume")
HYPOTHESES = ("H_client_close", "H_default_close")
WINDOW_KEYS = (
    "idle_timed_disconnects", "idle_other_cause_disconnects", "idle_connects", "idle_metadata_requests",
    "idle_request_timeouts", "first_close_after_idle_ms", "first_close_quiet_ms", "resume_idle_timed_disconnects",
    "resume_other_cause_disconnects", "resume_connects", "resume_request_timeouts", "resume_timeout_reconnect",
)
NULLABLE_WINDOW_KEYS = ("first_close_after_idle_ms", "first_close_quiet_ms")
BOOLEAN_WINDOW_KEYS = ("resume_timeout_reconnect",)
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
BLOB = REPO + "/blob/" + SOURCE + "/"

TEMPLATE = "\n".join([
    "# A04: with `connections.max.idle.ms=180000` each producer's first idle-window disconnect came before 225000 ms and every repeat was kept; with the key omitted, no repeat was kept",
    "",
    "In batch run 41602168787 on Event Hubs Premium, five fresh synthetic producers per configuration each produced one record, stayed idle for 420 s and produced again: C0 with `connections.max.idle.ms` omitted, so librdkafka leaves it unmodified, and C1 with `connections.max.idle.ms=180000`, the value Microsoft gives for Java clients and in its troubleshooting table but not in its librdkafka table; both set `metadata.max.age.ms` and `topic.metadata.refresh.interval.ms` to 900000. In every C1 repeat the first idle-window disconnect came @@C1_AFTER_MIN@@ ms to @@C1_AFTER_MAX@@ ms after idle start, after @@C1_QUIET_MIN@@ ms to @@C1_QUIET_MAX@@ ms of quiet, so H_client_close, which is decided by the timing of the first idle-window disconnect whichever side closed it, holds and every C1 repeat was `kept` (@@C1_CLASSES@@), while no C0 repeat was (@@C0_CLASSES@@). Because `connections.max.idle.ms` is absent from Microsoft's librdkafka table, the expected.yaml rules fix the verdict as `undocumented`, reason `not_in_librdkafka_table`.",
    "",
    "Proposed verdict: `undocumented`.",
    "",
    "## Approved inputs and run",
    "",
    "The [approved scenario](" + BLOB + "scenarios/A04/scenario.yaml) and [expected outcome](" + BLOB + "scenarios/A04/expected.yaml) are unchanged on the `main` this pull request is based on. Their decision rules were approved with the [idle observation judgement plan, Revision 6](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/319#issuecomment-6575747094) and its [pinned-code part](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/319#issuecomment-6579253743) by [plan check 6579727682](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/319#issuecomment-6579727682) and merged in [pull request 328](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/328) at 2026-11-02T16:39:43Z as `f0850d44086c4065cad76e64da6ec6f2d9cc50ce`, after the unpublished smoke run 41410644841, as the [split decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563452228), point 4, requires. The [metadata timers plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/329#issuecomment-6588394554), approved by [plan check 6588805136](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/329#issuecomment-6588805136), set both metadata timers to 900000 in [pull request 330](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/330), merged at 2026-11-02T22:06:44Z as `9014b8effd4f8556d3446bcfbf8f951a52905bb2`. The [idle resume timeout judgement plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/333#issuecomment-6596783115), approved by [plan check 6597891329](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/333#issuecomment-6597891329), added the `timed_out_on_resume` class in [pull request 336](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/336), merged at 2026-11-03T08:06:49Z as `c878a9ab2b2550e17ee4cbb5a6388f96f93d6c62`. All three plans were approved under standing approval: the [override decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563515099), point 2, allows it for issue 319, including expected outcomes, and the [rerun decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6588207250), point 2, and the [silent-drop decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6596426514), point 2, apply it to the A04 expected-outcome changes of issues 329 and 333.",
    "",
    "This report uses only [batch run 41602168787](" + RUN + "), attempt 1, event `workflow_dispatch`, on `main` at `c878a9ab2b2550e17ee4cbb5a6388f96f93d6c62`, created 2026-11-03T08:17:22Z, conclusion `success`, with `items` `A02,A04`. It is the run that point 4 of the silent-drop decision authorizes and that the [A04 decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/322#issuecomment-6596432846) names for publication; nothing is published for A04 from the earlier runs 41483886512 and 41534423570. Under the [verdict and bypass decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563540913), point 1, the verdict word is the one the merged decision rules select. The evidence is in two artifacts, both retained until 2027-02-01T08:17:23Z:",
    "",
    "- [candidate artifact 13218410622](" + RUN + "/artifacts/13218410622) (`idle-observation-candidates-41602168787-1`), ZIP SHA-256 `99b0f3e3ab24c97594ee775eadfc297c7885158390ee2925df5b0f8efb62ec63`;",
    "- [pod artifact 13218221146](" + RUN + "/artifacts/13218221146) (`idle-observation-pods-41602168787-1`), ZIP SHA-256 `2233339c5361c6dd2b23f4cb276aeab49976159155f69b8a435be1631b86b459`.",
    "",
    "The job logs are [batch job 125803257704](" + RUN + "/job/125803257704) (log SHA-256 `a747f60c3568fa287f91869c38dec5dc1706679b1dcb653b2c699f9b10b95632`) and [stop job 125826936334](" + RUN + "/job/125826936334) (log SHA-256 `76bd6dde11f5d8f866276bfdd3ec15d964d1e1dcd9d690b0660f0a71545fafe6`). The [candidate-derived result](2026-11-03.json) holds the A04 candidate and the judge receipt, equal in content to the artifact members `A04-idle-candidate.json` and `idle-judge-completion.json` and re-serialized with indentation. The generator [make_results.py](../../docs/operations/evidence/issue-322-a04-verdict/make_results.py) pins the SHA-256 of every input and wrote this report and that file; the [authored receipt](../../docs/operations/evidence/issue-322-a04-verdict/receipt.md) describes it.",
    "",
    "Each repeat is one fresh producer. The five producers of a configuration ran at the same time in one pod. Each produced one 1000-byte synthetic record to topic `document-events`, partition 0, stayed idle for 420 s with nothing to send while its callbacks were served, and then produced a second record, as the [idle-observation contract](../../docs/operations/idle-observation-contract.md) describes. C0 omits `connections.max.idle.ms`, the one approved exception to ADR-015's explicit-setting rule ([split decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563452228), point 2); the harness baseline sets it to 0. C1 sets it to 180000. Both set `metadata.max.age.ms` and `topic.metadata.refresh.interval.ms` to 900000, not the baseline's 180000 ms and 300000 ms, so that no metadata timer is due inside the idle period. Nothing else differs from the baseline; in particular `socket.keepalive.enable` is the baseline `true`, with the node's keepalive timers unchanged, and `request.timeout.ms` is the baseline 60000. Every repeat is `complete_valid`, so its captured settings equal the resolved baseline plus the scenario's selection (the `idle_settings` check of the verifier contract).",
    "",
    "## Repeat outcomes",
    "",
    "The verifier judged every repeat under the [idle-observation judgement](../../docs/operations/verifier-contract.md#idle-observation-judgement). A disconnect is classed only by its quiet period: the smaller of `txidle` and `rxidle` in the statistics report before the disconnect. It counts as `idle_timed` when that period is at least 225000 ms. That threshold is a harness choice, not a measurement: Microsoft's stated 240000 ms idle limit minus one 15000 ms report interval. No report names who closed a connection. A repeat is `closed_while_idle` when an `idle_timed` disconnect falls in the idle window, `stale_on_resume` when one falls only in the resume window, `timed_out_on_resume` when neither does but one broker shows a request timeout, then a disconnect, then a connect after the idle window, and `kept` otherwise; the first three are `not_kept`.",
    "",
    "<!-- tables:configurations -->",
    "",
    "<!-- tables:repeat-outcomes -->",
    "",
    "The idle window runs from the first report at or after idle start to the last report at or before idle end. The resume window runs from there to the first report at or after the second produce's outcome. Per expected.yaml, `other_cause` disconnects outside the resume timeout sequence, request timeouts outside it, the second produce's outcome, client error callbacks and the idle window's Metadata requests (`idle_metadata_requests`) are recorded per repeat below and select nothing.",
    "",
    "<!-- tables:windows -->",
    "",
    "<!-- tables:client-errors -->",
    "",
    "ADR-012 verifier counts (`ok`, `loss`, `phantom`, `reported_failure`, `duplicate` and `reorder`) are not applicable to this item and are absent, not zero, as `assessment_rules.adr012_counts` in expected.yaml states. The broker count of each client is `maximum_up_brokers`: the largest number, over its statistics reports, of brokers with source `learned`, `configured` or `logical` and state `UP`. It is in the repeat outcomes table, and the windows table gives the `connects` deltas.",
    "",
    "## Assessment against expected.yaml",
    "",
    "Microsoft's [client configuration page](https://github.com/MicrosoftDocs/azure-docs/blob/6da87e3c59251e962dd0b37dbd99ae364d767150/articles/event-hubs/apache-kafka-configurations.md) lists `connections.max.idle.ms` in its Java table with default 540000, recommended value 180000 and the constraint \"Must be < 240000\" (line 59), and its troubleshooting table gives \"Set `connections.max.idle.ms=180000` and `metadata.max.age.ms=180000`\" for \"Expired batches or send timeouts\" (line 133). Its librdkafka tables (lines 90 to 123) do not list the key. The page was read on 2026-11-03 at that revision, whose front matter has `ms.date: 12/17/2025`, and the [live page](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations), read the same day, has the same rows. librdkafka v2.16.0 documents `connections.max.idle.ms` as \"Close broker connections after the specified time of inactivity\", default 0, up to 2 s lower for jitter ([CONFIGURATION.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md), line 35).",
    "",
    "The precedence in expected.yaml is: \"@@PRECEDENCE@@\" The item rule is: \"@@DECISION@@\" The 163000 ms quiet floor is a harness choice, not a measurement: 180000 ms minus the 2000 ms jitter, minus one 15000 ms report interval.",
    "",
    "<!-- tables:hypotheses -->",
    "",
    "**H_client_close: `holds`.** Both configurations are `complete_valid`, no repeat is `first_failed`, and the five repeats of each configuration share one survival, so the precedence passes to the item rule. In every C1 repeat the first idle-window disconnect has `first_close_quiet_ms` from @@C1_QUIET_MIN@@ to @@C1_QUIET_MAX@@, at least 163000, and `first_close_after_idle_ms` from @@C1_AFTER_MIN@@ to @@C1_AFTER_MAX@@, below 225000, so H_client_close holds. The verdict is `undocumented`, reason `not_in_librdkafka_table`, whatever H_client_close shows, because the item rule fixes it.",
    "",
    "**H_default_close: recorded only.** C0 was `@@C0_SUMMARY@@` (@@C0_CLASSES@@). Its idle-window disconnects followed quiet periods of @@C0_CLOSE_MIN@@ ms to @@C0_CLOSE_MAX@@ ms; which side closed them is not recorded. librdkafka sets the key to 230000 ms when it is not modified and `bootstrap.servers` contains \"azure\" in any letter case ([rdkafka_conf.c L4682-L4687](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_conf.c#L4682-L4687)). That heuristic could not fire here. The batch log records the bootstrap server as `" + HOST + ":9093` (line 1806), and every broker name in every C0 and C1 statistics report is that host in mixed case; none contains \"azure\". The ephemeral layer rejects namespace names that contain \"azure\" ([variables.tf line 33](" + BLOB + "infra/ephemeral/variables.tf#L33)).",
    "",
    "The generator holds these rules, transcribed from the pinned expected.yaml. It recomputes every connection class from the window counts, each configuration summary from the classes and the item assessment from the summaries and the C1 window values, and requires each to equal the verifier's candidate.",
    "",
    "## The resume timeout class decided C0",
    "",
    "`timed_out_on_resume` was designed after batch run 41534423570 was seen, under the silent-drop decision, and is applied only to a later run: this run's source commit contains it, and run 41534423570 was judged at `9014b8effd4f8556d3446bcfbf8f951a52905bb2`, without it. In this run it decided C0. C0 repeat 3 recorded no disconnect in the idle window, although its largest quiet period was @@C0R3_MAX_QUIET@@ ms. Its second produce succeeded after @@C0R3_SECOND_MS@@ ms. In report 33, broker @@C0R3_NODE@@ recorded one request timeout, one disconnect after @@C0R3_QUIET@@ ms of quiet and one connect, the resume timeout sequence. Recomputed by the generator from the candidate's window counts, not by the verifier: under the rules at `9014b8effd4f8556d3446bcfbf8f951a52905bb2` that repeat would be `kept`, C0 `unequal`, and the item would have no verdict, reason `unequal_classes`. The silent-drop decision records the inference, not a measurement, that such a connection was already unusable after the idle period and that the client learned it only from the request timeout.",
    "",
    "## Disconnects recomputed from the statistics reports",
    "",
    "The generator recomputes, from every statistics report in the candidate, each rise of a broker's `disconnects` counter between consecutive reports, with the quiet period of that broker in the report before, in whole milliseconds as the verifier computes it, and the `req_timeouts` and `connects` rises in the same report. It also recomputes the largest quiet period on any broker in each repeat: @@MAX_C0_MIN@@ ms to @@MAX_C0_MAX@@ ms in C0 and @@MAX_C1_MIN@@ ms to @@MAX_C1_MAX@@ ms in C1. No broker's `req.Metadata` counter rose after the first report in any repeat, so `idle_metadata_requests` is 0 throughout. Each C1 repeat recorded two disconnects, on two brokers, each after less than 180000 ms of quiet, which the verifier classes `other_cause`.",
    "",
    "<!-- tables:largest-quiet -->",
    "",
    "<!-- tables:disconnects -->",
    "",
    "## Not tested",
    "",
    "Not tested: consumers, idle periods other than 420 s, `connections.max.idle.ms` values other than 180000, the baseline's explicit 0, the Azure heuristic's 230000 ms value, metadata timers other than 900000 ms, `socket.keepalive.enable=false` or any keepalive timer change, clients other than librdkafka 2.16.0 through Confluent.Kafka 2.16.0.0, tiers other than Premium, and which side closed a connection. Five repeats per configuration do not show how often an omitted-key connection fails only on resume.",
    "",
    "## Versions, environment and timing",
    "",
    "Confluent.Kafka `2.16.0.0`, from the candidate's `managed_version`, and librdkafka `2.16.0`, from its `native_version`. Image digests: loadgen `" + LOADGEN_DIGEST + "` and verifier `" + VERIFIER_DIGEST + "`, each also in its pod's `imageID`. The workload is synthetic.",
    "",
    "The batch job log records the planned ephemeral namespace as capacity 1 (line 1412), location `uksouth` (line 1421) and SKU `Premium` (line 1427), so the namespace used 1 PU in UK South. Node size `Standard_D4ds_v5` is inferred from all three pools of the [pinned cluster configuration](" + BLOB + "infra/cluster/aks.tf) and the cluster plan reporting no changes at 2026-11-03T08:32:12Z (line 915); no node SKU was captured directly. The ephemeral state was empty at 2026-11-03T09:13:44.6637054Z (batch log line 3749) and the AKS cluster was stopped at 2026-11-03T09:14:47.1492635Z (stop log line 343).",
    "",
    "<!-- tables:jobs -->",
    "",
    "The members are in pod artifact 13218221146. Each client ID is the pod name followed by `-r<repeat>`. The pod logs carry no line tagged by repeat, because the five clients share one pod. The per-repeat evidence is the candidate's repeat results, which keep every projected statistics report, and each pod JSON records the worker's exit code 0.",
    "",
    "## Mutation publication gate",
    "",
    "Current gate: [C5 run 41602164797](" + C5_RUN + "), attempt 1, measured `main` at `c878a9ab2b2550e17ee4cbb5a6388f96f93d6c62`, the source commit of this batch run, so the judge that produced the candidate is the code measured. Its [summary artifact 13220181257](" + C5_RUN + "/artifacts/13220181257) (`c5-summary-41602164797-1`), member `summary.json`, records `measurement_complete: true`, no incomplete reasons and `break_passed: true`, across all 18 shards of the seven configured targets, and an empty `rebalance_needed`. The break threshold is 60, from `thresholds.break` in `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` at that commit. The raw score is 97.7%, from 3724 / 3813 * 100 rounded to one decimal place. This meets the publication requirement in [verifier-mutation.md](../../docs/operations/verifier-mutation.md#issue-319-idle-observation-target), \"Issue 319 idle-observation target\", and point 4 of the silent-drop decision.",
    "",
    "<!-- tables:mutation-targets -->",
    "",
    "The run recorded 89 survivors, @@IDLE_SURVIVORS@@ of them in `VerifyRun/IdleObservation/IdleObservationJudgement.cs`, the target that judged this item. This pull request records no disposition for any of them and runs no survivor review or replay; dispositions recorded in [verifier-mutation.md](../../docs/operations/verifier-mutation.md) for other runs are not reapplied. `survivors.json` puts the first 50 survivors in its disposition sample and records `unreviewed_beyond_cap` 39, under the survivor review cap of 50 with exhaustion action `record the gap and continue`. `VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs` is byte-identical at `f0850d44086c4065cad76e64da6ec6f2d9cc50ce`, where C5 run 41484094658 recorded 8 survivors in it (the [A03 report](../A03/2026-11-02.md)), and at `c878a9ab2b2550e17ee4cbb5a6388f96f93d6c62`, where this run recorded 45. That difference is recorded here, not explained.",
    "",
    "<!-- tables:survivors -->",
])

TABLE_HEADERS = {
    "configurations": [
        "| Configuration | `connections.max.idle.ms` | `metadata.max.age.ms` | `topic.metadata.refresh.interval.ms` | Status | Summary | Capture SHA-256 |",
        "| --- | ---: | ---: | ---: | --- | --- | --- |",
    ],
    "repeat-outcomes": [
        "| Configuration | Repeat | Status | Connection class | First produce | First error code | First latency (ms) | Second produce | Second error code | Second latency (ms) | Max UP brokers |",
        "| --- | ---: | --- | --- | --- | ---: | ---: | --- | ---: | ---: | ---: |",
    ],
    "windows": [
        "| Configuration | Repeat | Idle idle_timed | Idle other_cause | Idle connects | Idle Metadata requests | Idle request timeouts | First close after idle (ms) | First close quiet (ms) | Resume idle_timed | Resume other_cause | Resume connects | Resume request timeouts | Resume timeout sequence | Client errors | Client errors dropped |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: |",
    ],
    "client-errors": [
        "| Configuration | Repeat | Code | Fatal | Origin | Observed elapsed (ms) |",
        "| --- | ---: | ---: | --- | --- | ---: |",
    ],
    "hypotheses": ["| Hypothesis | Status |", "| --- | --- |"],
    "largest-quiet": [
        "| Configuration | Repeat | Largest quiet period (ms) |",
        "| --- | ---: | ---: |",
    ],
    "disconnects": [
        "| Configuration | Repeat | Report | Report time (ms) | Broker node | Disconnects added | Quiet before disconnect (ms) | Request timeouts added | Connects added |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
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
        "| Mutant | Target | Line | Mutator | In disposition sample |",
        "| ---: | --- | ---: | --- | --- |",
    ],
}

OBSERVED = "With `connections.max.idle.ms=180000`, the client's first idle-window disconnect came @@C1_AFTER_MIN@@ ms to @@C1_AFTER_MAX@@ ms into a 420 s idle period, after @@C1_QUIET_MIN@@ ms to @@C1_QUIET_MAX@@ ms of quiet, and all five repeats were `kept` (@@C1_CLASSES@@); with the key omitted, none was (@@C0_CLASSES@@). The key is absent from Microsoft's librdkafka table, so the verdict is fixed as `undocumented`. The `timed_out_on_resume` repeat counts as not kept only under a class designed after an earlier run was seen; without it the item would have no verdict."


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
    expected = ["A02-idle-candidate.json", "A04-idle-candidate.json", "idle-judge-completion.json"]
    expect(listing == expected, "G1", "candidates directory holds " + ",".join(listing))
    for key, pin in PINS.items():
        expect(key in inputs, "G1", "missing input " + key)
        expect(sha256(inputs[key]) == pin, "G1", "SHA-256 differs for " + key)


def check_g2(inputs):
    receipt = parse(inputs, "cand/idle-judge-completion.json", "G2")
    candidate = parse(inputs, "cand/A04-idle-candidate.json", "G2")
    exact_keys(receipt, RECEIPT_KEYS, "G2", "receipt")
    judge = PODS["judge"]
    expect(receipt["schema_version"] == 1 and receipt["mode"] == "idle-observation-judge", "G2", "receipt schema or mode")
    expect(receipt["run_id"] == RUN_ID and receipt["source_commit"] == SOURCE, "G2", "receipt run or commit")
    expect(receipt["job_name"] == judge[0], "G2", "receipt job")
    expect(receipt["pod_name"] == judge[1] and receipt["pod_uid"] == judge[2], "G2", "receipt pod")
    expect(is_int(receipt["handler_exit_code"]) and receipt["handler_exit_code"] == 0, "G2", "receipt exit code")
    expect(receipt["cleanup_completed"] is True, "G2", "receipt cleanup")
    entries = receipt["candidates"]
    expect(isinstance(entries, list) and [e.get("scenario_id") for e in entries] == ["A02", "A04"], "G2", "receipt candidates")
    for entry in entries:
        expect(entry.get("status") == "complete_valid", "G2", "receipt entry status " + str(entry.get("scenario_id")))
        name = entry["scenario_id"] + "-idle-candidate.json"
        expect(("cand/" + name) in inputs and entry.get("candidate_sha256") == sha256(inputs["cand/" + name]), "G2", "receipt hash for " + name)
    entry = entries[1]
    expect(entry.get("candidate_name") == RUN_ID + "/idle-observation/A04/judge/candidate.json", "G2", "receipt A04 candidate name")
    assessment = candidate.get("item_assessment") if isinstance(candidate, dict) else None
    expect(isinstance(assessment, dict) and entry.get("reason") == assessment.get("reason"), "G2", "receipt A04 reason")
    return receipt


def check_g3(candidate):
    exact_keys(candidate, CANDIDATE_KEYS, "G3", "candidate")
    expect(candidate["schema_version"] == 1 and candidate["mode"] == "idle-observation-judge", "G3", "schema or mode")
    expect(candidate["synthetic"] is True and candidate["run_kind"] == "batch", "G3", "synthetic or run_kind")
    expect(candidate["run_id"] == RUN_ID and candidate["scenario_id"] == "A04", "G3", "run or scenario")
    expect(candidate["source_commit"] == SOURCE, "G3", "source commit")
    expect(candidate["managed_version"] == "2.16.0.0" and candidate["native_version"] == "2.16.0", "G3", "versions")
    expect(candidate["image_digests"] == {"loadgen": LOADGEN_DIGEST, "verifier": VERIFIER_DIGEST}, "G3", "image digests")
    expect(candidate["adr012_counts"] == "not_applicable", "G3", "adr012_counts")
    expect(candidate["notes"] == [], "G3", "notes")
    exact_keys(candidate["item_assessment"], ("verdict", "reason", "hypotheses"), "G3", "item_assessment")
    exact_keys(candidate["item_assessment"]["hypotheses"], HYPOTHESES, "G3", "hypotheses")


def check_g4(candidate):
    assessment = candidate["item_assessment"]
    wanted = ("undocumented", "not_in_librdkafka_table", {"H_client_close": "holds", "H_default_close": "recorded_only"})
    expect((assessment["verdict"], assessment["reason"], assessment["hypotheses"]) == wanted, "G4",
           "outcome is " + str(assessment["verdict"]) + ", " + str(assessment["reason"]) + ", " + json.dumps(assessment["hypotheses"])
           + "; the report text exists only for undocumented, not_in_librdkafka_table with H_client_close holds")


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
                    if key in BOOLEAN_WINDOW_KEYS:
                        expect(isinstance(value, bool), "G5", label + " windows." + key)
                    else:
                        expect(is_int(value) or (key in NULLABLE_WINDOW_KEYS and value is None), "G5", label + " windows." + key)
            expect(is_int(repeat["maximum_up_brokers"]) and repeat["maximum_up_brokers"] >= 0, "G5", label + " brokers")
            expect(isinstance(repeat["reports"], list), "G5", label + " reports")
            expect(isinstance(repeat["client_errors"], list), "G5", label + " client_errors")
            for row in repeat["client_errors"]:
                exact_keys(row, ERROR_ROW_KEYS, "G5", label + " client error row")


def classify(repeat, resume_timeout=True):
    if repeat["first"]["result"] != "success":
        return "first_failed"
    windows = repeat["windows"]
    if windows["idle_timed_disconnects"] > 0:
        return "closed_while_idle"
    if windows["resume_idle_timed_disconnects"] > 0:
        return "stale_on_resume"
    if resume_timeout and windows["resume_timeout_reconnect"]:
        return "timed_out_on_resume"
    return "kept"


def summarize(classes):
    if "first_failed" in classes:
        return "first_failed"
    if all(c == "kept" for c in classes):
        return "kept"
    if all(c in NOT_KEPT for c in classes):
        return "not_kept"
    return "unequal"


def hypotheses(decided):
    if decided is None:
        return {name: "undecided" for name in HYPOTHESES}
    return {name: (decided if name == HYPOTHESES[0] else "recorded_only") for name in HYPOTHESES}


def client_close_holds(repeats):
    for repeat in repeats:
        windows = repeat["windows"]
        quiet, after = windows["first_close_quiet_ms"], windows["first_close_after_idle_ms"]
        if quiet is None or after is None or quiet < 163000 or after >= 225000:
            return False
    return True


def item_rule(summaries, c1_repeats):
    values = [summaries[c] for c in CONFIGS]
    if "first_failed" in values:
        return ("untestable", "first_produce_failed", hypotheses(None))
    if "unequal" in values:
        return (None, "unequal_classes", hypotheses(None))
    return ("undocumented", "not_in_librdkafka_table", hypotheses("holds" if client_close_holds(c1_repeats) else "contradicted"))


def check_g6(candidate):
    summaries = {}
    prior = {}
    by_id = {}
    for configuration in candidate["configurations"]:
        cid = configuration["configuration_id"]
        by_id[cid] = configuration
        classes = []
        for repeat in configuration["repeats"]:
            expect(classify(repeat) == repeat["connection_class"], "G6", cid + " repeat " + str(repeat["repeat"]) + " class differs from recomputation")
            classes.append(repeat["connection_class"])
        expect(summarize(classes) == configuration["summary"], "G6", cid + " summary differs from recomputation")
        summaries[cid] = configuration["summary"]
        prior[cid] = summarize([classify(r, resume_timeout=False) for r in configuration["repeats"]])
    verdict, reason, statuses = item_rule(summaries, by_id["C1"]["repeats"])
    assessment = candidate["item_assessment"]
    expect((assessment["verdict"], assessment["reason"], assessment["hypotheses"]) == (verdict, reason, statuses), "G6", "assessment differs from recomputation")
    expect(item_rule(prior, by_id["C1"]["repeats"])[0:2] == PRIOR_RULE, "G6", "prior-rule outcome differs from " + str(PRIOR_RULE))


def check_g7(inputs):
    status = parse(inputs, "pods/phase-status.json", "G7")
    batch = parse(inputs, "pods/idle-observation-batch.json", "G7")
    expect(status.get("phase") == "idle-observation" and status.get("run_id") == RUN_ID, "G7", "phase or run")
    expect(status.get("safety_stop") is False, "G7", "safety stop")
    expect(status.get("not_selected") == [{"item": "A03", "configuration": c} for c in ("C0", "C1", "C2")], "G7", "not_selected")
    modes = {m.get("job"): m for m in status.get("modes", [])}
    for cid in CONFIGS:
        mode = modes.get(PODS[cid][0])
        expect(mode is not None, "G7", "no mode for " + cid)
        expect(mode.get("outcome") == "complete" and mode.get("collect") == 0, "G7", cid + " outcome or collect")
        expect(mode.get("item") == "A04" and mode.get("configuration") == cid and mode.get("role") == "loadgen", "G7", cid + " identity")
        expect(mode.get("capture_complete") is True, "G7", cid + " capture")
        expect(mode.get("capture_pod") == {"name": PODS[cid][1], "uid": PODS[cid][2]}, "G7", cid + " capture pod")
    judge = modes.get(PODS["judge"][0])
    expect(judge is not None, "G7", "no judge mode")
    expect(judge.get("mode") == "idle-observation-judge" and judge.get("outcome") == "complete" and judge.get("collect") == 0, "G7", "judge mode")
    entries = [e for e in status.get("candidates", []) if e.get("item") == "A04"]
    expect(entries == [{"item": "A04", "configuration": "judge", "accepted": True, "transport_valid": True, "candidate": "A04-idle-candidate.json", "judge_completion": "idle-judge-completion.json"}], "G7", "A04 candidate entry")
    expect(batch.get("schema_version") == 1 and batch.get("mode") == "idle-observation" and batch.get("run_kind") == "batch", "G7", "batch identity")
    expect(batch.get("selected_items") == ["A02", "A04"] and batch.get("candidates") == ["A02", "A04"], "G7", "batch items")
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

    expect("+ terraform -chdir=infra/cluster plan " in at(batch, 889), "G9", "batch line 889")
    expect(at(batch, 915) == "2026-11-03T08:32:12.8117519Z No changes. Your infrastructure matches the configuration.", "G9", "batch line 915")
    line = at(batch, 1409)
    expect("module.eventhubs.azurerm_eventhub_namespace.this[0" in line and "will be created" in line, "G9", "batch line 1409")
    expect(at(batch, 1412).endswith("+ capacity                                  = 1"), "G9", "batch line 1412")
    expect(at(batch, 1421).endswith('+ location                                  = "uksouth"'), "G9", "batch line 1421")
    expect(at(batch, 1427).endswith('+ sku                                       = "Premium"'), "G9", "batch line 1427")
    expect(at(batch, 1806).endswith('+ bootstrap_servers = "' + HOST + ':9093"'), "G9", "batch line 1806")
    expect(at(batch, 3749) == "2026-11-03T09:13:44.6637054Z ephemeral_state_empty", "G9", "batch line 3749")
    expect(at(stop, 343) == "2026-11-03T09:14:47.1492635Z aks_stopped", "G9", "stop line 343")


def check_g10(inputs):
    summary = parse(inputs, "c5/summary.json", "G10")
    survivors = parse(inputs, "c5/survivors.json", "G10")
    expect(summary.get("commit") == SOURCE and summary.get("run_id") == "41602164797" and summary.get("run_attempt") == "1", "G10", "summary identity")
    expect(summary.get("measurement_complete") is True and summary.get("incomplete_reasons") == [], "G10", "measurement complete")
    expect(summary.get("break_passed") is True, "G10", "break passed")
    expect(summary.get("rebalance_needed") == [], "G10", "rebalance_needed")
    expect(summary.get("numerator") == COMBINED[7] and summary.get("denominator") == COMBINED[8], "G10", "summary score")
    expect(isinstance(summary.get("shards"), dict) and len(summary["shards"]) == SHARD_COUNT, "G10", "shard count")
    targets = summary.get("targets")
    expect(isinstance(targets, dict) and list(targets) == [row[0] for row in TARGETS], "G10", "summary populations")
    for row in TARGETS:
        entry = targets[row[0]]
        expect(entry.get("target") == row[1], "G10", "target path for " + row[0])
        expect([entry["status_counts"].get(name) for name in STATUS_NAMES] == list(row[2:9]), "G10", "status counts for " + row[0])
        expect(entry.get("numerator") == row[9] and entry.get("denominator") == row[10], "G10", "score for " + row[0])
    expect([summary["status_counts"].get(name) for name in STATUS_NAMES] == list(COMBINED[:7]), "G10", "combined status counts")
    config = json.loads(git_show("tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json", SOURCE, "G10"))["stryker-config"]
    expect(config["thresholds"]["break"] == 60, "G10", "break threshold")
    expect(config["mutate"] == [row[1] for row in TARGETS], "G10", "mutate list")
    expect(is_ancestor(PRIOR_C5_COMMIT, SOURCE), "G10", "prior C5 commit is not an ancestor of the run source")
    expect(git_rev("src/Parchwell.Verifier/" + SAC, PRIOR_C5_COMMIT) == git_rev("src/Parchwell.Verifier/" + SAC, SOURCE), "G10", "configuration judgement differs between the two C5 commits")
    prior = json.loads(git_show("results/A03/2026-11-02.json", SOURCE, "G10"))["publication"]["mutation_gate"]
    expect(prior["measured_commit"] == PRIOR_C5_COMMIT, "G10", "A03 mutation gate commit")
    expect(sum(1 for s in prior["survivors"] if s["target"] == SAC) == PRIOR_SAC_SURVIVORS, "G10", "A03 configuration judgement survivor count")
    exact_keys(survivors, ("survivors", "disposition_sample", "unreviewed_beyond_cap"), "G10", "survivors.json")
    rows = survivors["survivors"]
    expect(len(rows) == COMBINED[2], "G10", "survivor count")
    expect(survivors["disposition_sample"] == rows[:SAMPLE_SIZE], "G10", "disposition sample is not the first 50 survivors")
    expect(survivors["unreviewed_beyond_cap"] == UNREVIEWED == len(rows) - SAMPLE_SIZE, "G10", "unreviewed_beyond_cap")
    expect(len({str(item.get("id")) for item in rows}) == len(rows), "G10", "duplicate survivor id")
    for item in rows:
        expect(item.get("status") == "Survived", "G10", "survivor status")
    for row in TARGETS:
        expect(sum(1 for item in rows if item.get("file") == row[1]) == row[4], "G10", "survivor count for " + row[1])
    return summary


def check_g11(sources, expected):
    for path, pin in SOURCES.items():
        committed = git_show(path)
        expect(sha256(committed) == pin, "G11", "committed " + path)
        if path in WORKING_TREE_SOURCES:
            expect(sources[path] == committed, "G11", "working tree differs for " + path)
    data = expected
    expect(sha256(data) == SOURCES["scenarios/A04/expected.yaml"], "G11", "expected.yaml bytes differ from the pin")
    lines = data.decode("utf-8").split("\n")
    for line in EXPECTED_LINES:
        expect(lines.count(line) == 1, "G11", "expected.yaml line missing or repeated: " + line[:60])
    lines = sources["scenarios/A04/scenario.yaml"].decode("utf-8").split("\n")
    for line, count in SCENARIO_LINES.items():
        expect(lines.count(line) == count, "G11", "scenario.yaml line count differs: " + line)
    lines = git_show("src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs").decode("utf-8").split("\n")
    for line in BASELINE_LINES:
        expect(lines.count(line) == 1, "G11", "BaselineSettingsCatalog.cs line missing or repeated: " + line[:60])
    lines = git_show("infra/ephemeral/variables.tf").decode("utf-8").split("\n")
    expect(len(lines) > 32 and lines[32] == VARIABLES_LINE, "G11", "infra/ephemeral/variables.tf line 33")


def quiet_ms(broker):
    if broker is None:
        return 0
    tx, rx = broker["tx_idle_us"], broker["rx_idle_us"]
    if tx == -1 and rx == -1:
        return 0
    micros = rx if tx == -1 else tx if rx == -1 else min(tx, rx)
    return micros // 1000


def broker_host(key):
    match = re.fullmatch(r"sasl_ssl://([^:/]+):9093/-?[0-9]+", key)
    return match.group(1).lower() if match else None


def check_g12(candidate):
    maxima = {}
    disconnects = []
    steps = []
    for configuration in candidate["configurations"]:
        cid = configuration["configuration_id"]
        maxima[cid] = []
        for repeat in configuration["repeats"]:
            reports = repeat["reports"]
            label = cid + " repeat " + str(repeat["repeat"])
            for report in reports:
                for key in report["brokers"]:
                    expect(broker_host(key) == HOST, "G12", label + " broker name " + key)
                    expect("azure" not in key.lower(), "G12", label + " broker name contains azure")
            maxima[cid].append(max([quiet_ms(b) for report in reports for b in report["brokers"].values()] or [0]))
            first = []
            for index in range(1, len(reports)):
                before = reports[index - 1]["brokers"]
                for key, broker in reports[index]["brokers"].items():
                    earlier = before.get(key)
                    rose = broker["disconnects"] - (earlier["disconnects"] if earlier else 0)
                    if rose > 0:
                        timeouts = broker["req_timeouts"] - (earlier["req_timeouts"] if earlier else 0)
                        connects = broker["connects"] - (earlier["connects"] if earlier else 0)
                        row = (cid, repeat["repeat"], reports[index]["sequence"], reports[index]["observed_elapsed_ms"],
                               broker["node_id"], rose, quiet_ms(earlier), timeouts, connects)
                        disconnects.append(row)
                        first.append(row)
                    added = broker["metadata"] - (earlier["metadata"] if earlier else 0)
                    if added > 0:
                        steps.append((cid, repeat["repeat"], reports[index]["sequence"], reports[index]["observed_elapsed_ms"], added))
            windows = repeat["windows"]
            expect(windows["idle_metadata_requests"] == 0, "G12", label + " idle Metadata requests")
            if windows["first_close_quiet_ms"] is not None:
                expect(first and windows["first_close_quiet_ms"] == min(r[6] for r in first if r[2] == first[0][2]), "G12", label + " first close quiet differs from the first disconnect report")
    expect(maxima == REPEAT_MAX_QUIET, "G12", "largest quiet periods differ")
    expect(disconnects == DISCONNECTS, "G12", "disconnect rows differ")
    expect(steps == METADATA_STEPS, "G12", "Metadata request rows differ")


def read_sources():
    return {path: (ROOT / path).read_bytes() for path in WORKING_TREE_SOURCES}


def verify(inputs, pins=True, sources=None):
    if pins:
        check_g1(inputs)
    receipt = check_g2(inputs)
    candidate = parse(inputs, "cand/A04-idle-candidate.json", "G3")
    check_g3(candidate)
    check_g4(candidate)
    check_g5(candidate)
    check_g6(candidate)
    check_g7(inputs)
    check_g8(inputs)
    check_g9(inputs)
    summary = check_g10(inputs)
    sources = sources if sources is not None else read_sources()
    check_g11(sources, sources["scenarios/A04/expected.yaml"])
    check_g12(candidate)
    survivors = parse(inputs, "c5/survivors.json", "G10")
    return {"candidate": candidate, "receipt": receipt, "summary": summary, "survivors": survivors, "inputs": inputs}


def survivor_rows(ctx):
    sample = {str(item["id"]) for item in ctx["survivors"]["disposition_sample"]}
    rows = [(int(item["id"]), item["file"], item["location"]["start"]["line"], item["mutatorName"], str(item["id"]) in sample)
            for item in ctx["survivors"]["survivors"]]
    return sorted(rows)


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


def setting_value(value):
    return value if value == "omitted" else int(value)


def render_json(ctx):
    candidate = ctx["candidate"]
    assessment = candidate["item_assessment"]
    survivors = [{"id": m, "target": t, "line": line, "mutator": mutator, "in_disposition_sample": sampled}
                 for m, t, line, mutator, sampled in survivor_rows(ctx)]
    document = {
        "publication": {
            "item": "A04", "proposed_verdict": assessment["verdict"], "reason": assessment["reason"],
            "hypotheses": assessment["hypotheses"],
            "verdict_authority": "issue 301 decision 6563540913 point 1, adopted on issue 322 by decision 6563546354; run choice: issue 301 decision 6596426514 point 4, adopted on issue 322 by decision 6596432846",
            "workload": "synthetic", "adr012_counts": "not_applicable", "notes": [],
            "settings": {c: {"connections.max.idle.ms": setting_value(SETTINGS[c][0]), "metadata.max.age.ms": int(SETTINGS[c][1]),
                             "topic.metadata.refresh.interval.ms": int(SETTINGS[c][2])} for c in CONFIGS},
            "recomputed": {
                "observed_summaries": {c["configuration_id"]: c["summary"] for c in candidate["configurations"]},
                "prior_rule": {"rules_commit": "9014b8effd4f8556d3446bcfbf8f951a52905bb2", "verdict": PRIOR_RULE[0], "reason": PRIOR_RULE[1]},
                "repeat_max_quiet_ms": REPEAT_MAX_QUIET,
                "disconnects": [{"configuration": r[0], "repeat": r[1], "report": r[2], "report_elapsed_ms": r[3], "node_id": r[4],
                                 "disconnects_added": r[5], "quiet_before_ms": r[6], "request_timeouts_added": r[7],
                                 "connects_added": r[8]} for r in DISCONNECTS],
                "metadata_requests_after_first_report": METADATA_STEPS,
                "broker_host": HOST,
            },
            "run": {
                "workflow_run": RUN, "attempt": 1, "run_id": RUN_ID, "source_commit": SOURCE, "run_kind": "batch",
                "items": ["A02", "A04"], "conclusion": "success", "created_at": "2026-11-03T08:17:22Z",
                "candidate_artifact": {"id": 13218410622, "name": "idle-observation-candidates-41602168787-1", "zip_sha256": "99b0f3e3ab24c97594ee775eadfc297c7885158390ee2925df5b0f8efb62ec63"},
                "pod_artifact": {"id": 13218221146, "name": "idle-observation-pods-41602168787-1", "zip_sha256": "2233339c5361c6dd2b23f4cb276aeab49976159155f69b8a435be1631b86b459"},
                "artifacts_expire_at": "2027-02-01T08:17:23Z", "batch_job_id": 125803257704, "stop_job_id": 125826936334,
                "batch_log_sha256": PINS["logs/batch.log"], "stop_log_sha256": PINS["logs/stop.log"],
            },
            "rules": {
                "scenario_yaml_sha256": SOURCES["scenarios/A04/scenario.yaml"],
                "expected_yaml_sha256": SOURCES["scenarios/A04/expected.yaml"],
                "approvals": [
                    {"issue": 319, "plan": "comments 6575747094 and 6579253743, Revision 6", "plan_check": 6579727682, "pull_request": 328,
                     "merged_at": "2026-11-02T16:39:43Z", "merge_commit": "f0850d44086c4065cad76e64da6ec6f2d9cc50ce"},
                    {"issue": 329, "plan": "comment 6588394554, Revision 2", "plan_check": 6588805136, "pull_request": 330,
                     "merged_at": "2026-11-02T22:06:44Z", "merge_commit": "9014b8effd4f8556d3446bcfbf8f951a52905bb2"},
                    {"issue": 333, "plan": "comment 6596783115, Revision 2", "plan_check": 6597891329, "pull_request": 336,
                     "merged_at": "2026-11-03T08:06:49Z", "merge_commit": SOURCE},
                ],
            },
            "environment": {
                "region": "uksouth", "event_hubs_sku": "Premium", "pu_count": 1, "node_size": "Standard_D4ds_v5",
                "node_size_evidence": "inferred_from_pinned_configuration_and_no_change_cluster_plan",
                "bootstrap_servers": HOST + ":9093",
                "managed_version": "2.16.0.0", "native_version": "2.16.0",
                "image_digests": {"loadgen": LOADGEN_DIGEST, "verifier": VERIFIER_DIGEST},
                "ephemeral_state_empty_at": "2026-11-03T09:13:44.6637054Z", "aks_stopped_at": "2026-11-03T09:14:47.1492635Z",
            },
            "jobs": job_rows(),
            "mutation_gate": {
                "workflow_run": C5_RUN, "attempt": 1,
                "summary_artifact": {"id": 13220181257, "name": "c5-summary-41602164797-1", "zip_sha256": "db220b179377791eda68796f8d6eb011906cc1eee44084f6a25cc850af7ae08a"},
                "summary_json_sha256": PINS["c5/summary.json"], "survivors_json_sha256": PINS["c5/survivors.json"],
                "measured_commit": SOURCE, "measurement_complete": True, "break_passed": True, "break_threshold": 60,
                "numerator": COMBINED[7], "denominator": COMBINED[8], "shards": SHARD_COUNT,
                "disposition_sample_size": SAMPLE_SIZE, "unreviewed_beyond_cap": UNREVIEWED,
                "dispositions_recorded_in_this_pull_request": 0, "survivors": survivors,
            },
        },
        "judge_receipt": ctx["receipt"],
        "candidate": candidate,
    }
    return (json.dumps(document, indent=2, ensure_ascii=True) + "\n").encode()


def cell(value, ticks=False):
    if value is None:
        return "none"
    if isinstance(value, bool):
        return "yes" if value else "no"
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
            lines.append(row([cid] + list(SETTINGS[cid]) + [cell(c["status"], True), cell(c["summary"], True), cell(c["capture_sha256"], True)]))
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
    elif name == "largest-quiet":
        for cid in CONFIGS:
            for index, value in enumerate(REPEAT_MAX_QUIET[cid]):
                lines.append(row([cid, str(index + 1), str(value)]))
    elif name == "disconnects":
        for r in DISCONNECTS:
            lines.append(row([r[0]] + [str(v) for v in r[1:]]))
    elif name == "jobs":
        for j in job_rows():
            lines.append(row([j["configuration"], j["stage"], "`" + j["job"] + "` (`" + j["account"] + "`)", j["container_started"],
                              j["container_finished"], "`" + j["pod_log_member"] + "`", "`" + j["pod_json_member"] + "`"]))
    elif name == "mutation-targets":
        for t in TARGETS:
            lines.append(row(["`" + t[1] + "`"] + [str(v) for v in t[2:9]] + [str(t[9]) + " / " + str(t[10])]))
        lines.append(row(["All seven targets"] + [str(v) for v in COMBINED[:7]] + [str(COMBINED[7]) + " / " + str(COMBINED[8])]))
    elif name == "survivors":
        for mutant, target, line, mutator, sampled in survivor_rows(ctx):
            lines.append(row([str(mutant), "`" + target + "`", str(line), mutator, "yes" if sampled else "no"]))
    return TABLE_HEADERS[name] + lines


def tokens(ctx):
    by_id = {c["configuration_id"]: c for c in ctx["candidate"]["configurations"]}
    c1 = [r["windows"] for r in by_id["C1"]["repeats"]]
    c0_closes = [r["windows"]["first_close_quiet_ms"] for r in by_id["C0"]["repeats"] if r["windows"]["first_close_quiet_ms"] is not None]
    r3 = by_id["C0"]["repeats"][2]
    row3 = [r for r in DISCONNECTS if r[0] == "C0" and r[1] == 3]
    values = {
        "C0_CLASSES": class_counts(by_id["C0"]), "C1_CLASSES": class_counts(by_id["C1"]),
        "C0_SUMMARY": by_id["C0"]["summary"], "DECISION": DECISION, "PRECEDENCE": PRECEDENCE,
        "C1_AFTER_MIN": str(min(w["first_close_after_idle_ms"] for w in c1)), "C1_AFTER_MAX": str(max(w["first_close_after_idle_ms"] for w in c1)),
        "C1_QUIET_MIN": str(min(w["first_close_quiet_ms"] for w in c1)), "C1_QUIET_MAX": str(max(w["first_close_quiet_ms"] for w in c1)),
        "C0_CLOSE_MIN": str(min(c0_closes)), "C0_CLOSE_MAX": str(max(c0_closes)),
        "C0R3_MAX_QUIET": str(REPEAT_MAX_QUIET["C0"][2]), "C0R3_SECOND_MS": str(r3["second"]["latency_ms"]),
        "C0R3_NODE": str(row3[0][4]), "C0R3_QUIET": str(row3[0][6]),
        "IDLE_SURVIVORS": str(TARGETS[6][4]),
    }
    for cid in CONFIGS:
        values["MAX_" + cid + "_MIN"] = str(min(REPEAT_MAX_QUIET[cid]))
        values["MAX_" + cid + "_MAX"] = str(max(REPEAT_MAX_QUIET[cid]))
    return values


def fill(text, values):
    for key, value in values.items():
        text = text.replace("@@" + key + "@@", value)
    return text


def readme_row(ctx):
    return "| A04 | Idle connection configuration | " + fill(OBSERVED, tokens(ctx)) + " | undocumented | [Report](results/A04/2026-11-03.md) |"


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
    for name, data in (("2026-11-03.json", render_json(ctx)), ("2026-11-03.md", render_md(ctx))):
        path = ROOT / "results" / "A04" / name
        expect(path.is_file() and path.read_bytes() == data, "G13", "results/A04/" + name + " differs from the rendered bytes")
    lines = (ROOT / "README.md").read_text(encoding="utf-8").split("\n")
    expect(len(lines) > 15 and lines[15] == readme_row(ctx), "G13", "README.md line 16 differs")


def edit_json(inputs, key, change):
    value = strict_json(inputs[key])
    change(value)
    inputs[key] = json.dumps(value, separators=(",", ":")).encode()


def refresh_receipt(inputs):
    def change(receipt):
        receipt["candidates"][1]["candidate_sha256"] = sha256(inputs["cand/A04-idle-candidate.json"])
        receipt["candidates"][1]["reason"] = strict_json(inputs["cand/A04-idle-candidate.json"])["item_assessment"]["reason"]
    edit_json(inputs, "cand/idle-judge-completion.json", change)


def candidate_edit(change):
    return lambda i: edit_json(i, "cand/A04-idle-candidate.json", change)


def self_test(inputs, sources):
    verify(inputs, pins=True, sources=sources)
    cases = []

    def case(name, code, mutate, pins=False, refresh=False):
        cases.append((name, code, mutate, pins, refresh))

    def append_newline(i):
        i["cand/A04-idle-candidate.json"] += b"\n"

    def add_extra(i):
        i["cand-listing"] = json.dumps(sorted(json.loads(i["cand-listing"]) + ["extra.json"])).encode()

    def break_hash(receipt):
        entry = receipt["candidates"][1]
        entry["candidate_sha256"] = entry["candidate_sha256"][:-1] + ("0" if entry["candidate_sha256"][-1] != "0" else "1")

    def set_quiet(c):
        broker = next(iter(c["configurations"][1]["repeats"][0]["reports"][0]["brokers"].values()))
        broker["tx_idle_us"] = broker["rx_idle_us"] = 999999999

    def drop_disconnect(c):
        reports = c["configurations"][0]["repeats"][0]["reports"]
        for key, broker in reports[19]["brokers"].items():
            if key in reports[18]["brokers"]:
                broker["disconnects"] = reports[18]["brokers"][key]["disconnects"]

    def rename_broker(c):
        brokers = c["configurations"][1]["repeats"][0]["reports"][0]["brokers"]
        key = next(iter(brokers))
        brokers["sasl_ssl://azure-" + key[len("sasl_ssl://"):]] = brokers.pop(key)

    def restart(i):
        edit_json(i, "pods/" + PODS["C1"][0] + "/pods.json", lambda p: p["items"][0]["status"]["containerStatuses"][0].update(restartCount=1))

    def gap(i):
        edit_json(i, "pods/" + PODS["judge"][0] + "/collection-status.json", lambda s: s.update(gaps=["x"]))

    def replace_line(number, text):
        def change(i):
            lines = i["logs/batch.log"].decode("utf-8").split("\n")
            lines[number - 1] = text
            i["logs/batch.log"] = "\n".join(lines).encode()
        return change

    def drop_survivor(s):
        s["survivors"].pop()

    case("G1-append", "G1", append_newline, pins=True)
    case("G1-extra", "G1", add_extra, pins=True)
    case("G2-exit", "G2", lambda i: edit_json(i, "cand/idle-judge-completion.json", lambda r: r.update(handler_exit_code=1)))
    case("G2-hash", "G2", lambda i: edit_json(i, "cand/idle-judge-completion.json", break_hash))
    case("G3-notes", "G3", candidate_edit(lambda c: c.update(notes=["k37_not_tested"])), refresh=True)
    case("G3-kind", "G3", candidate_edit(lambda c: c.update(run_kind="smoke")), refresh=True)
    case("G4-hypothesis", "G4", candidate_edit(lambda c: c["item_assessment"]["hypotheses"].update(H_client_close="contradicted")), refresh=True)
    case("G5-status", "G5", candidate_edit(lambda c: c["configurations"][1]["repeats"][2].update(status="incomplete")), refresh=True)
    case("G5-client", "G5", candidate_edit(lambda c: c["configurations"][0]["repeats"][0].update(client_id="x-r1")), refresh=True)
    case("G6-class", "G6", candidate_edit(lambda c: c["configurations"][0]["repeats"][2].update(connection_class="kept")), refresh=True)
    case("G6-summary", "G6", candidate_edit(lambda c: c["configurations"][1].update(summary="not_kept")), refresh=True)
    case("G6-floor", "G6", candidate_edit(lambda c: c["configurations"][1]["repeats"][1]["windows"].update(first_close_quiet_ms=162999)), refresh=True)
    case("G7-stop", "G7", lambda i: edit_json(i, "pods/phase-status.json", lambda s: s.update(safety_stop=True)))
    case("G7-evidence", "G7", lambda i: edit_json(i, "pods/idle-observation-batch.json", lambda b: b.update(evidence_passed=False)))
    case("G8-restart", "G8", restart)
    case("G8-gap", "G8", gap)
    case("G9-teardown", "G9", replace_line(3749, "x"))
    case("G9-bootstrap", "G9", replace_line(1806, '      + bootstrap_servers = "eh-azure.servicebus.windows.net:9093"'))
    case("G10-break", "G10", lambda i: edit_json(i, "c5/summary.json", lambda s: s.update(break_passed=False)))
    case("G10-commit", "G10", lambda i: edit_json(i, "c5/summary.json", lambda s: s.update(commit=PRIOR_C5_COMMIT)))
    case("G10-survivor", "G10", lambda i: edit_json(i, "c5/survivors.json", drop_survivor))
    case("G12-max", "G12", candidate_edit(set_quiet), refresh=True)
    case("G12-disconnect", "G12", candidate_edit(drop_disconnect), refresh=True)
    case("G12-azure", "G12", candidate_edit(rename_broker), refresh=True)
    case("G12-window", "G12", candidate_edit(lambda c: c["configurations"][1]["repeats"][4]["windows"].update(idle_metadata_requests=1)), refresh=True)

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
    for name, change in (("G11-expected", lambda s: check_g11(s, s["scenarios/A04/expected.yaml"] + b"x")),
                         ("G11-scenario", lambda s: check_g11(dict(s, **{"scenarios/A04/scenario.yaml": s["scenarios/A04/scenario.yaml"] + b"x"}), s["scenarios/A04/expected.yaml"]))):
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
    parser = argparse.ArgumentParser(description="Assemble the A04 publication offline.")
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
        out = ROOT / "results" / "A04"
        out.mkdir(parents=True, exist_ok=True)
        (out / "2026-11-03.json").write_bytes(json_bytes)
        (out / "2026-11-03.md").write_bytes(md_bytes)
        return 0
    except CheckFailure as error:
        print("FAIL " + error.code + ": " + error.message, file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

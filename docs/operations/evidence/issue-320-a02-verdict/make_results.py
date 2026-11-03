"""Assemble the A02 publication from batch run 41602168787 and mutation campaign 41670913768, offline.

Purpose: build results/A02/2026-11-03.json and results/A02/2026-11-03.md, the
candidate-derived result and report for the A02 verdict pull request (issue 320),
from the idle-observation batch run 41602168787 and the C5 mutation campaign
41670913768. Nothing here reaches Azure or GitHub.

Command line:

    python3 -I docs/operations/evidence/issue-320-a02-verdict/make_results.py \
        --candidates DIR --pods DIR --c5 DIR --batch-log FILE --stop-log FILE \
        [--check | --readme-row | --self-test]

Supported inputs: the extracted candidate and pod artifacts of attempt 1, the
extracted C5 summary artifact of campaign 41670913768, the two job logs, and this
checkout's git objects for c878a9a and 87327f5.

Output: with no mode flag it writes only results/A02/2026-11-03.json and
results/A02/2026-11-03.md. It is not a second scientific validator. The
verifier's candidate stays authoritative; the transcribed rules only confirm it.

Failure conditions (exit 1, "FAIL G<n>: <message>" on stderr):
  G1  an input is missing, extra, or does not match its pinned SHA-256
  G2  the judge receipt does not match the run, pod, or candidate
  G3  the candidate identity, versions, digests, notes or keys differ
  G4  the outcome is not `contradicted`, `closed_with_keepalive`
  G5  a configuration or repeat has the wrong shape, status or client ID
  G6  a recomputed class, summary, verdict, reason or hypothesis differs
  G7  the phase status or batch record differs
  G8  a pod, container, exit code, image digest or time differs, or the repeat-tagged
      client log lines of a pod log differ from the pinned counts
  G9  a pinned job-log line differs
  G10 the mutation summary, shard, isolated mutants, survivor set or sample differs
  G10a the campaign commit differs from the batch source in src or scenarios, or any
      .cs or .csproj file differs between them
  G11 a source file or a pinned source line differs
  G12 a recomputed quiet maximum, disconnect, timeout, reconnect or count differs
  G13 (--check only) a generated file or README line 14 differs

Regression example: copy the candidates directory, append a newline to
A02-idle-candidate.json and run with --check: G1 fails before anything is written.
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
C5_RUN = REPO + "/actions/runs/41670913768"
C5_RUN_ID = "41670913768"
C5_COMMIT = "87327f5841822a8962dd3dbaecb2ee442a8faa8b"
C5_TREE = "0acdbc11a962d054420e1fab1f97b7c4a4ff390b"
C5_ARTIFACT = {"id": 13256330812, "name": "c5-summary-41670913768-1", "zip_sha256": "fdd649f14cd26fffbb58bae37b260cba2cc3ac44c628db991d13141491cedfa9"}
# Per-shard counts of mutants the campaign isolated from static-field leakage (pull request 341, issue 340).
ISOLATED_MUTANTS = {"publication": 11, "workload": 4, "single-action-configuration-p1": 7, "single-action-configuration-p4": 12, "idle-observation-p1": 8}
# Client log lines tagged by repeat in the pod logs: (configuration, repeat) -> number of REQTMOUT lines.
TAGGED_CLIENT_LOGS = {("C0", 1): 2, ("C1", 3): 2}
LOADGEN_DIGEST = "sha256:c43ca9fc2c5ac759cef722f0fe0506058d3064d6e21b88749dbad9c223350750"
VERIFIER_DIGEST = "sha256:971906cace285b307da14a97386a158d425b09035aab14c0052e6d6adcbb3f27"
CONFIGS = ("C0", "C1")
PODS = {
    "C0": ("idle-41602168787-1-a02-c0-idle", "idle-41602168787-1-a02-c0-idle-tfmgz", "07217aea-a0c5-4070-b1af-be35e9137316", "loadgen"),
    "C1": ("idle-41602168787-1-a02-c1-idle", "idle-41602168787-1-a02-c1-idle-l55br", "695fa182-1a8a-43b9-8e8e-1cbdc9791bc9", "loadgen"),
    "judge": ("idle-41602168787-1-idle-judge", "idle-41602168787-1-idle-judge-sr4jp", "bb945a74-3ae8-48bb-9271-8ed0808fb185", "verifier"),
}
KEEPALIVE = {"C0": "false", "C1": "true"}
CONTAINER_TIMES = {
    "C0": ("2026-11-03T08:36:43Z", "2026-11-03T08:45:08Z"),
    "C1": ("2026-11-03T08:45:18Z", "2026-11-03T08:53:41Z"),
    "judge": ("2026-11-03T09:10:07Z", "2026-11-03T09:10:10Z"),
}
REPEAT_MAX_QUIET = {"C0": [419281, 284287, 284293, 284299, 284306], "C1": [284863, 284637, 419823, 284735, 359835]}
# (configuration, repeat, report sequence, broker node_id, disconnects added, quiet ms of that broker in the
# previous report, req_timeouts added, connects added)
EVENTS = [
    ("C0", 1, 33, 8, 1, 59179, 1, 1), ("C0", 2, 20, 8, 1, 284287, 0, 0), ("C0", 3, 20, 8, 1, 284293, 0, 0),
    ("C0", 4, 20, 8, 1, 284299, 0, 0), ("C0", 5, 20, 8, 1, 284306, 0, 0), ("C1", 1, 20, 8, 1, 284863, 0, 0),
    ("C1", 2, 20, 8, 1, 284637, 0, 0), ("C1", 3, 33, 8, 1, 59747, 1, 1), ("C1", 4, 20, 8, 1, 284735, 0, 0),
    ("C1", 5, 25, 8, 1, 359835, 0, 0),
]
SHARDS = [
    "phase", "publication", "a01", "workload", "single-action-repeat-p1", "single-action-repeat-p2", "single-action-repeat-p3",
    "single-action-configuration-p1", "single-action-configuration-p2", "single-action-configuration-p3", "single-action-configuration-p4",
    "single-action-configuration-p5", "single-action-configuration-p6", "single-action-configuration-p7", "single-action-configuration-p8",
    "idle-observation-p1", "idle-observation-p2", "idle-observation-p3",
]
IDLE_TIMED_MS = 225000

PINS = {
    "cand/A02-idle-candidate.json": "7a2116c68f2ed3faf94d717a9b815416f983ea9569d2ca965ac6e797eeb86c8b",
    "cand/A04-idle-candidate.json": "c8342147d98d9636c7e3fa3438c8c1c921e0ac33758ad2552e0a3c9db69ea023",
    "cand/idle-judge-completion.json": "261851eb60b505014e4efc771e9fc5db255204256eb261ec55b5824ce6a5fa53",
    "pods/phase-status.json": "ad9c0deb8d910ca9d509dd7bb547db0211aaf7f17af602377fd6a60020b822a7",
    "pods/idle-observation-batch.json": "87aa7db27b96a88fe4b53423b07c219293770a4681390d32b0fe9a1f334640d7",
    "pods/idle-41602168787-1-a02-c0-idle/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "pods/idle-41602168787-1-a02-c0-idle/idle-41602168787-1-a02-c0-idle-tfmgz.containers.loadgen.log": "196263a3874d0b75410d43fda480b4ddcd1dfd9170d336709b4c4d567976a2a8",
    "pods/idle-41602168787-1-a02-c0-idle/pods.json": "e5188b4d8ebb1f2bc5dc35055e5b02fc96345a622107a539a10a9da270999594",
    "pods/idle-41602168787-1-a02-c1-idle/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "pods/idle-41602168787-1-a02-c1-idle/idle-41602168787-1-a02-c1-idle-l55br.containers.loadgen.log": "300e7e1143777f6367b3e844a835d61fffcc95fc9200bc47801bad7e81372c94",
    "pods/idle-41602168787-1-a02-c1-idle/pods.json": "cedfe76c6fb8a8038dadc60f45b758805bde7b5c5ad74584a767f72ac586a61f",
    "pods/idle-41602168787-1-idle-judge/collection-status.json": "fa4da065bc94ef89ab52608d18a9f5f1ccb6e652c02d37ac85100f3d663a4966",
    "pods/idle-41602168787-1-idle-judge/idle-41602168787-1-idle-judge-sr4jp.containers.verifier.log": "197871f001e6cea52c11df077e495c9b5f1d8e8997a25206c79cc617d4226326",
    "pods/idle-41602168787-1-idle-judge/pods.json": "c3122871c794ea4c1b4cde8021d206c91d7c281220d715a949d4293c11e28b3c",
    "c5/summary.json": "d8cdfb8a17482c5eec35315df9e9d3ac102cc47fe9cca4d6d5e0cdb36a6507f8",
    "c5/summary.md": "6690d0f09bb82d122ef5f142e5d40c24360267e3406d079c39f184d561b1691d",
    "c5/survivors.json": "f90dab7447263feb19d95bb36b456678167e378ef0e8c02fe846d0b17daf4e02",
    "logs/batch.log": "a747f60c3568fa287f91869c38dec5dc1706679b1dcb653b2c699f9b10b95632",
    "logs/stop.log": "76bd6dde11f5d8f866276bfdd3ec15d964d1e1dcd9d690b0660f0a71545fafe6",
}

SOURCES = {
    "scenarios/A02/scenario.yaml": "739b96191574afff8edd89edbb9b09452185d636afa6add915888de8c3e70493",
    "scenarios/A02/expected.yaml": "924e740c1ef5bc1de6a26c7647e7dcfc239ca906b174766159edc4ac50043a89",
    "infra/cluster/aks.tf": "969762e923eae813525207d50f5689de9fbed20c0c4c2c274367bcb54462a8a2",
}
WORKING_TREE_SOURCES = ("scenarios/A02/scenario.yaml", "scenarios/A02/expected.yaml")

# (population, target, parts, Killed, Timeout, Survived, NoCoverage, CompileError, RuntimeError, Ignored, numerator, denominator)
TARGETS = [
    ("phase", "Judge/PhaseJudge.cs", 1, 189, 1, 16, 0, 4, 0, 6, 190, 206),
    ("publication", "VerifyRun/Publication/PublicationAcceptance.cs", 1, 176, 0, 7, 0, 6, 0, 14, 176, 183),
    ("a01", "VerifyRun/A01/A01EvidenceRecorder.cs", 1, 71, 0, 0, 0, 15, 0, 8, 71, 71),
    ("workload", "VerifyRun/A01/WorkloadRecorder.cs", 1, 67, 0, 28, 0, 2, 0, 7, 67, 95),
    ("single-action-repeat", "VerifyRun/SingleAction/SingleActionRepeatJudgement.cs", 3, 792, 0, 8, 0, 142, 0, 29, 792, 800),
    ("single-action-configuration", "VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs", 8, 1948, 0, 63, 0, 158, 0, 46, 1948, 2011),
    ("idle-observation", "VerifyRun/IdleObservation/IdleObservationJudgement.cs", 3, 424, 0, 23, 0, 101, 0, 15, 424, 447),
]
STATUS_NAMES = ("Killed", "Timeout", "Survived", "NoCoverage", "CompileError", "RuntimeError", "Ignored")
# Killed, Timeout, Survived, NoCoverage, CompileError, RuntimeError, Ignored, numerator, denominator
COMBINED = (3667, 1, 145, 0, 428, 0, 125, 3668, 3813)

DECISION = "over the survival in assessment_rules: C0 kept and C1 not_kept -> null, `inverse_effect`. Else C1 not_kept -> contradicted, `closed_with_keepalive`. Else C0 not_kept -> confirmed, `keepalive_kept_connection`. Else (both kept) -> untestable, `no_idle_close_observed`."
PRECEDENCE = "smoke run -> null, `smoke_only`; a missing candidate or any configuration not `complete_valid` -> null, `incomplete_configuration`; any repeat `first_failed` -> `untestable`, `first_produce_failed`; any configuration whose five repeats do not share one survival -> null, `unequal_classes`; then the item rule."
EXPECTED_LINES = [
    '  decision: "' + DECISION + '"',
    '  precedence: "' + PRECEDENCE + '"',
    "  connection_classes: [kept, closed_while_idle, stale_on_resume, timed_out_on_resume, first_failed]",
    '    expected: "not_kept in C1 as in C0, an inference from K-35"',
    '    reading: "Microsoft\'s note \\"Required to prevent idle connection closure\\" is read as a claim that true prevents the closure; necessity alone is not tested"',
    "  metadata_timers_ms: 900000",
    '  text_must_state: "the 225000 ms idle_timed threshold is a harness choice, not a measurement; per repeat, the other_cause disconnects, request timeouts, second produce outcome, client error callbacks and idle_metadata_requests. Both metadata timers were 900000 ms in C0 and C1, so C1 differs from Microsoft\'s recommended metadata.max.age.ms of 180000 ms and the result does not test Microsoft\'s full librdkafka configuration. The timed_out_on_resume class was designed after batch run 41534423570 was seen and is applied only to a later run."',
]

CLASSES = ("kept", "closed_while_idle", "stale_on_resume", "timed_out_on_resume", "first_failed")
HYPOTHESES = ("H_keepalive", "H_resume")
WINDOW_KEYS = (
    "idle_timed_disconnects", "idle_other_cause_disconnects", "idle_connects", "idle_metadata_requests",
    "idle_request_timeouts", "first_close_after_idle_ms", "first_close_quiet_ms", "resume_idle_timed_disconnects",
    "resume_other_cause_disconnects", "resume_connects", "resume_request_timeouts", "resume_timeout_reconnect",
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
RECEIPT_ENTRY_KEYS = ("scenario_id", "candidate_name", "candidate_sha256", "status", "reason")
SELECTION = {"not_selected": [{"item": "A03", "configuration": "C0"}, {"item": "A03", "configuration": "C1"}, {"item": "A03", "configuration": "C2"}]}
MUTATION_DOC = "../../docs/operations/verifier-mutation.md"

TEMPLATE = r"""# A02: producer connections were not kept across a 420 s idle period with `socket.keepalive.enable=true`, as without it

In batch run 41602168787 on Event Hubs Premium, five fresh synthetic producers per configuration each produced one record, stayed idle for 420 s and produced again: C0 with `socket.keepalive.enable=false`, the librdkafka default, and C1 with `true`, Microsoft's recommendation, both with the node's keepalive timers unchanged and both metadata timers at 900000 ms. No producer kept its connections in either configuration (C0: @@C0_CLASSES@@; C1: @@C1_CLASSES@@), so the expected.yaml rules select `contradicted`, reason `closed_with_keepalive`, for Microsoft's note that `true` is "Required to prevent idle connection closure". Whether Event Hubs counts TCP keepalive probes as activity (K-37) was not tested, and this result implies no answer to it.

Proposed verdict: `contradicted`. The merged decision rules select it for this run.

## Approved inputs and run

The [approved scenario](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/c878a9ab2b2550e17ee4cbb5a6388f96f93d6c62/scenarios/A02/scenario.yaml) and [expected outcome](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/c878a9ab2b2550e17ee4cbb5a6388f96f93d6c62/scenarios/A02/expected.yaml) are unchanged on the `main` this pull request is based on. Their decision rules were approved with the [idle observation judgement plan, Revision 6](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/319#issuecomment-6575747094) and its [pinned-code part](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/319#issuecomment-6579253743) by [plan check 6579727682](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/319#issuecomment-6579727682) under standing approval, which the [override decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563515099), point 2, allows, and merged in [pull request 328](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/328) at 2026-11-02T16:39:43Z, after the unpublished smoke run 41410644841, as the [split decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563452228), point 4, requires. Under the [rerun decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6588207250), the [metadata timers plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/329#issuecomment-6588394554), approved under standing approval by [plan check 6588805136](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/329#issuecomment-6588805136), set both metadata timers to 900000 ms in C0 and C1; it merged in [pull request 330](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/330) at 2026-11-02T22:06:44Z. Under [decision 6596426514](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6596426514), the [idle resume timeout judgement plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/333#issuecomment-6596783115), approved under standing approval by [plan check 6597891329](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/333#issuecomment-6597891329), added the `timed_out_on_resume` class and the rule-history statement in the assessment below; it merged in [pull request 336](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/336) at 2026-11-03T08:06:49Z as `c878a9ab2b2550e17ee4cbb5a6388f96f93d6c62`, before this run was created.

This report uses only [batch run 41602168787](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41602168787), attempt 1, event `workflow_dispatch`, on `main` at `c878a9ab2b2550e17ee4cbb5a6388f96f93d6c62`, with `items` `A02,A04`, created 2026-11-03T08:17:22Z, conclusion `success`. Nothing is published for A02 from the earlier batch runs 41483886512 and 41534423570, under the rerun decision and decision 6596426514. Under the [verdict and bypass decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563540913), point 1, which issue 320 adopts, the verdict word is the one the merged decision rules select. The evidence is in two artifacts, both retained until 2027-02-01T08:17:23Z:

- [candidate artifact 13218410622](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41602168787/artifacts/13218410622) (`idle-observation-candidates-41602168787-1`), ZIP SHA-256 `99b0f3e3ab24c97594ee775eadfc297c7885158390ee2925df5b0f8efb62ec63`;
- [pod artifact 13218221146](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41602168787/artifacts/13218221146) (`idle-observation-pods-41602168787-1`), ZIP SHA-256 `2233339c5361c6dd2b23f4cb276aeab49976159155f69b8a435be1631b86b459`.

The job logs are [batch job 125803257704](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41602168787/job/125803257704) (log SHA-256 `a747f60c3568fa287f91869c38dec5dc1706679b1dcb653b2c699f9b10b95632`) and [stop job 125826936334](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41602168787/job/125826936334) (log SHA-256 `76bd6dde11f5d8f866276bfdd3ec15d964d1e1dcd9d690b0660f0a71545fafe6`). The [candidate-derived result](2026-11-03.json) holds the A02 candidate and the judge receipt, equal in content to the artifact members `A02-idle-candidate.json` and `idle-judge-completion.json` and re-serialized with indentation. The generator [make_results.py](../../docs/operations/evidence/issue-320-a02-verdict/make_results.py) pins the SHA-256 of every input and wrote this report and that file; the [authored receipt](../../docs/operations/evidence/issue-320-a02-verdict/receipt.md) describes it.

Each repeat is one fresh producer. The five producers of a configuration ran at the same time in one pod. Each produced one 1000-byte synthetic record to topic `document-events`, partition 0, stayed idle for 420 s with nothing to send while its callbacks were served, and then produced a second record, as the [idle-observation contract](../../docs/operations/idle-observation-contract.md) describes. C0 sets `socket.keepalive.enable` to `false`, the librdkafka default. C1 keeps the harness baseline `true`, with the node's keepalive timers unchanged and no pod sysctl. Both set `metadata.max.age.ms` and `topic.metadata.refresh.interval.ms` to 900000, so that no metadata timer is due inside the idle period. Nothing else differs. Both metadata timers were 900000 ms in C0 and C1, so C1 differs from Microsoft's recommended `metadata.max.age.ms` of 180000 ms and the result does not test Microsoft's full librdkafka configuration.

## Repeat outcomes

The verifier judged every repeat under the [idle-observation judgement](../../docs/operations/verifier-contract.md#idle-observation-judgement). A disconnect is classed only by its quiet period: the smaller of `txidle` and `rxidle` in the statistics report before the disconnect. It counts as `idle_timed` when that period is at least 225000 ms. That threshold is a harness choice, not a measurement: Microsoft's stated 240000 ms idle limit minus one 15000 ms report interval. No report names who closed a connection. The resume timeout sequence is, on one broker connection, a request timeout, then a disconnect, then a new connection attempt, each counted in a report after the idle window and no later than the first report at or after the second produce's outcome. A repeat is `closed_while_idle` when an `idle_timed` disconnect falls in the idle window, `stale_on_resume` when one falls only in the resume window, `timed_out_on_resume` when neither does and the resume timeout sequence occurs, and `kept` otherwise; the first three are `not_kept`.

<!-- tables:configurations -->

<!-- tables:repeat-outcomes -->

The idle window runs from the first report at or after idle start to the last report at or before idle end. The resume window runs from there to the first report at or after the second produce's outcome. Per expected.yaml, `other_cause` disconnects and request timeouts outside the resume timeout sequence, the second produce's outcome, client error callbacks and the idle window's Metadata requests are recorded per repeat below and select nothing.

<!-- tables:windows -->

<!-- tables:client-errors -->

ADR-012 verifier counts (`ok`, `loss`, `phantom`, `reported_failure`, `duplicate` and `reorder`) are not applicable to this item and are absent, not zero, as `assessment_rules.adr012_counts` in expected.yaml states. The broker count of each client is `maximum_up_brokers`: the largest number, over its statistics reports, of brokers with source `learned`, `configured` or `logical` and state `UP`. It is in the repeat outcomes table, and the window tables give the `connects` deltas.

## Assessment against expected.yaml

Microsoft's [client configuration page](https://github.com/MicrosoftDocs/azure-docs/blob/6da87e3c59251e962dd0b37dbd99ae364d767150/articles/event-hubs/apache-kafka-configurations.md) lists `socket.keepalive.enable` for librdkafka with default `false`, recommended value `true` and the note "Required to prevent idle connection closure" (line 100). The `metadata.max.age.ms` row below it says "Azure closes idle connections after 240 seconds" (line 101). Both lines were read on 2026-11-03 at that page revision, whose front matter has `ms.date: 12/17/2025`, and the [live page](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations) read the same day has the same rows. expected.yaml reads the note "as a claim that true prevents the closure; necessity alone is not tested".

The precedence in expected.yaml is: "@@PRECEDENCE@@" The item rule is: "@@DECISION@@"

<!-- tables:hypotheses -->

**Verdict: `contradicted`, reason `closed_with_keepalive`.** Both configurations are `complete_valid` and no repeat is `first_failed`. In each configuration the five repeats share one survival, `not_kept`, so the precedence passes to the item rule. C0 is not `kept`, so `inverse_effect` does not apply; C1 is `not_kept`, so the rule gives `contradicted`, reason `closed_with_keepalive`. The candidate records H_keepalive as `contradicted`. H_resume is recorded only: the second produce's outcome and latency per repeat are in the repeat outcomes table.

This matches the expected outcome in expected.yaml: `not_kept` in C1 as in C0, an inference from K-35.

The generator holds these rules, transcribed from the pinned expected.yaml. It recomputes every connection class from the window counts, each configuration summary from the classes and the assessment from the summaries, and requires each to equal the verifier's candidate.

The `timed_out_on_resume` class was designed after batch run 41534423570 was seen and is applied only to a later run, as expected.yaml states. In this run it decides the verdict. Under the class rule before it, C0 repeat @@TO_REPEAT_C0@@ and C1 repeat @@TO_REPEAT_C1@@ would have been `kept`, each configuration `unequal`, and the precedence would have selected no verdict, reason `unequal_classes`. The rules are applied as merged before this run was created.

## Connections closed after the idle period

In C0 repeat @@TO_REPEAT_C0@@ and C1 repeat @@TO_REPEAT_C1@@, one broker connection stayed `UP` with no disconnect through the idle window, and its quiet period reached @@TO_MAX_C0@@ ms and @@TO_MAX_C1@@ ms, above the 225000 ms threshold. After the second produce was sent, one statistics report recorded on that connection a request timeout, a disconnect and a new connection attempt: the resume timeout sequence. The quiet period in the report before that disconnect was @@TO_Q_C0@@ ms and @@TO_Q_C1@@ ms, below the threshold, so the disconnect itself counts as `other_cause`. The client reconnected, and the second record was delivered with a latency of @@TO_LAT_C0@@ ms and @@TO_LAT_C1@@ ms. Inferred, not measured, as the verifier contract states: such a connection was already unusable after the idle period, and the client learned it only from the request timeout.

In the other eight repeats, the one `idle_timed` disconnect in the idle window followed a quiet period of @@CLOSE_MIN@@ ms to @@CLOSE_MAX@@ ms, with no request timeout, and the second produce succeeded with a latency of @@CW_LAT_MIN@@ ms to @@CW_LAT_MAX@@ ms. Every idle window recorded 0 Metadata requests. Which side closed any connection is not recorded.

The generator recomputes, from every statistics report in the candidate, the largest quiet period of each repeat on any broker, in whole milliseconds as the verifier computes it, and every disconnect and request timeout, and checks them against the verifier's window counts. The table lists each such event.

<!-- tables:events -->

## Not tested

Whether Event Hubs counts TCP keepalive probes as activity (K-37) was not tested, and this result implies no answer to it. No configuration changed the node's keepalive timers. With the AKS default keepalive time of 7200 s recorded in K-35 of the [research catalogue](../../docs/research/kubernetes-kafka-clients.md), no keepalive probe is expected inside a 420 s idle period; that is inferred, not observed. Both metadata timers were 900000 ms, so Microsoft's full librdkafka configuration, with `metadata.max.age.ms` 180000, was not tested. Also not tested: a pod sysctl or any other keepalive timer, consumers, idle periods other than 420 s, clients other than librdkafka 2.16.0 through Confluent.Kafka 2.16.0.0, tiers other than Premium, and which side closed a connection.

## Versions, environment and timing

Confluent.Kafka `2.16.0.0`, from the candidate's `managed_version`, and librdkafka `2.16.0`, from its `native_version`. Image digests: loadgen `sha256:c43ca9fc2c5ac759cef722f0fe0506058d3064d6e21b88749dbad9c223350750` and verifier `sha256:971906cace285b307da14a97386a158d425b09035aab14c0052e6d6adcbb3f27`, each also in its pod's `imageID`. The workload is synthetic.

The batch job log records the planned ephemeral namespace as capacity 1 (line 1412), location `uksouth` (line 1421) and SKU `Premium` (line 1427), so the namespace used 1 PU in UK South. Node size `Standard_D4ds_v5` is inferred from all three pools of the [pinned cluster configuration](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/c878a9ab2b2550e17ee4cbb5a6388f96f93d6c62/infra/cluster/aks.tf) and the cluster plan reporting no changes at 2026-11-03T08:32:12Z (line 915); no node SKU was captured directly. The ephemeral state was empty at 2026-11-03T09:13:44.6637054Z (batch log line 3749) and the AKS cluster was stopped at 2026-11-03T09:14:47.1492635Z (stop log line 343).

<!-- tables:jobs -->

The members are in pod artifact 13218221146. Each client ID is the pod name followed by `-r<repeat>`. Client log lines in the pod logs are tagged by repeat (`idle_observation_client_log r<repeat>`); in this run only C0 repeat 1 and C1 repeat 3 have such lines, two `REQTMOUT` lines each. The per-repeat evidence is the candidate's repeat results, which keep every projected statistics report, and each pod JSON records the worker's exit code 0.

## Mutation publication gate

Current gate: [C5 run 41670913768](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41670913768), attempt 1, measured `main` at `87327f5841822a8962dd3dbaecb2ee442a8faa8b` (tree `0acdbc11a962d054420e1fab1f97b7c4a4ff390b`), a later commit than this batch run's source `c878a9ab2b2550e17ee4cbb5a6388f96f93d6c62`. `git diff --quiet c878a9a 87327f5 -- src scenarios` exits 0, and no `.cs` or `.csproj` file differs between them (both checked 2026-11-03; the generator checks both as G10a). Only `tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py`, its test and documentation changed, so the campaign measured the verifier code that judged this item (docs/agents/factory.md "Evidence pack", mutation acceptance: a passing run on a later main commit is acceptable when it includes the target changes under review). Its [summary artifact 13256330812](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41670913768/artifacts/13256330812) (`c5-summary-41670913768-1`), member `summary.json`, records `measurement_complete: true`, no incomplete reasons and `break_passed: true`, across all 18 shards of the seven configured targets, and lists no shard in `rebalance_needed`. Five shards also record `isolated_mutants`, 42 mutant IDs in total (publication 11, workload 4, single-action-configuration part 1 7 and part 4 12, idle-observation part 1 8). The break threshold is 60, from `thresholds.break` in `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` at that commit. The raw score is 96.2%, from 3668 / 3813 * 100 rounded to one decimal place. This meets the publication requirement in [verifier-mutation.md](../../docs/operations/verifier-mutation.md#issue-319-idle-observation-target), "Issue 319 idle-observation target", and point 4 of decision 6596426514. Campaign 41602164797 is dropped as a gate and not cited as evidence: it was superseded because its shards ran static-field mutants in a way that leaked state between mutants (pull request 341, issue 340).

<!-- tables:mutation-targets -->

The campaign left 145 surviving mutants in six targets, 23 of them in `VerifyRun/IdleObservation/IdleObservationJudgement.cs`, the target that judged this item. No survivor of this campaign is dispositioned in this pull request. 61 of the 145 are newly exposed survivors that the earlier campaign had counted as killed through the leakage (the issue 340 comparison, an inference stated there). `survivors.json` lists the first 50 survivors as its disposition sample, which is not a disposition, and records the other 95 as `unreviewed_beyond_cap`, under the survivor review cap of 50 and its exhaustion action `record the gap and continue` in [verifier-mutation.md](../../docs/operations/verifier-mutation.md). Whether that gap is acceptable for this verdict is Hari's decision when he reviews this pull request. Separately from the mutation measurement, the generator recomputes every connection class, both configuration summaries and the assessment from the candidate's window counts, and checks those counts against the disconnects, request timeouts and connection attempts in the candidate's own statistics reports.

<!-- tables:survivors -->"""

TABLE_HEADERS = {
    "configurations": [
        "| Configuration | `socket.keepalive.enable` | Metadata timers (ms) | Status | Summary | Capture SHA-256 |",
        "| --- | --- | ---: | --- | --- | --- |",
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
    "events": [
        "| Configuration | Repeat | Largest quiet period (ms) | Report | Broker node | Disconnects added | Quiet before (ms) | Request timeouts added | Connection attempts added |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ],
    "jobs": [
        "| Configuration | Stage | Job (account) | Container started | Container finished | Pod log member | Pod JSON member |",
        "| --- | --- | --- | --- | --- | --- | --- |",
    ],
    "mutation-targets": [
        "| Target | Parts | Killed | Timeout | Survived | NoCoverage | CompileError | RuntimeError | Ignored | Detected / scored |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ],
    "survivors": [
        "| Mutant | Target | Line | Mutator | In disposition sample |",
        "| ---: | --- | ---: | --- | --- |",
    ],
}

OBSERVED = r"""With `socket.keepalive.enable=true`, Microsoft's recommendation, no producer kept its connections across a 420 s idle period in five repeats (@@C1_CLASSES@@), and with `false` none did either (@@C0_CLASSES@@). Only disconnects after at least 225000 ms of quiet, or a request timeout, disconnect and reconnect after the idle period, count; the second rule was designed after an earlier run was seen. Both metadata timers were 900000 ms, not Microsoft's recommended 180000 ms. Whether Event Hubs counts keepalive probes as activity was not tested."""


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


def check_g1(inputs):
    listing = json.loads(inputs["cand-listing"])
    expected = ["A02-idle-candidate.json", "A04-idle-candidate.json", "idle-judge-completion.json"]
    expect(listing == expected, "G1", "candidates directory holds " + ",".join(listing))
    for key, pin in PINS.items():
        expect(key in inputs, "G1", "missing input " + key)
        expect(sha256(inputs[key]) == pin, "G1", "SHA-256 differs for " + key)


def check_g2(inputs):
    receipt = parse(inputs, "cand/idle-judge-completion.json", "G2")
    candidate = parse(inputs, "cand/A02-idle-candidate.json", "G2")
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
        exact_keys(entry, RECEIPT_ENTRY_KEYS, "G2", "receipt entry " + str(entry.get("scenario_id")))
        expect(entry["status"] == "complete_valid", "G2", "receipt entry status " + entry["scenario_id"])
        name = entry["scenario_id"] + "-idle-candidate.json"
        expect(("cand/" + name) in inputs and entry["candidate_sha256"] == sha256(inputs["cand/" + name]), "G2", "receipt hash for " + name)
    entry = entries[0]
    expect(entry["candidate_name"] == RUN_ID + "/idle-observation/A02/judge/candidate.json", "G2", "receipt A02 candidate name")
    assessment = candidate.get("item_assessment") if isinstance(candidate, dict) else None
    expect(isinstance(assessment, dict) and entry["reason"] == assessment.get("reason"), "G2", "receipt A02 reason")
    return receipt


def check_g3(candidate):
    exact_keys(candidate, CANDIDATE_KEYS, "G3", "candidate")
    expect(candidate["schema_version"] == 1 and candidate["mode"] == "idle-observation-judge", "G3", "schema or mode")
    expect(candidate["synthetic"] is True and candidate["run_kind"] == "batch", "G3", "synthetic or run_kind")
    expect(candidate["run_id"] == RUN_ID and candidate["scenario_id"] == "A02", "G3", "run or scenario")
    expect(candidate["source_commit"] == SOURCE, "G3", "source commit")
    expect(candidate["managed_version"] == "2.16.0.0" and candidate["native_version"] == "2.16.0", "G3", "versions")
    expect(candidate["image_digests"] == {"loadgen": LOADGEN_DIGEST, "verifier": VERIFIER_DIGEST}, "G3", "image digests")
    expect(candidate["adr012_counts"] == "not_applicable", "G3", "adr012_counts")
    expect(candidate["notes"] == ["k37_not_tested"], "G3", "notes")
    exact_keys(candidate["item_assessment"], ("verdict", "reason", "hypotheses"), "G3", "item_assessment")
    exact_keys(candidate["item_assessment"]["hypotheses"], HYPOTHESES, "G3", "hypotheses")


def check_g4(candidate):
    assessment = candidate["item_assessment"]
    expect(assessment["verdict"] == "contradicted" and assessment["reason"] == "closed_with_keepalive", "G4",
           "outcome is " + str(assessment["verdict"]) + ", " + str(assessment["reason"]) + "; the report text exists only for contradicted, closed_with_keepalive")


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
                    if key == "resume_timeout_reconnect":
                        expect(isinstance(value, bool), "G5", label + " windows." + key)
                    else:
                        expect(is_int(value) or (key in NULLABLE_WINDOW_KEYS and value is None), "G5", label + " windows." + key)
            expect(is_int(repeat["maximum_up_brokers"]) and repeat["maximum_up_brokers"] >= 0, "G5", label + " brokers")
            expect(is_int(repeat["client_errors_dropped"]), "G5", label + " client_errors_dropped")
            expect(isinstance(repeat["reports"], list), "G5", label + " reports")
            expect(isinstance(repeat["client_errors"], list), "G5", label + " client_errors")
            for row in repeat["client_errors"]:
                exact_keys(row, ERROR_ROW_KEYS, "G5", label + " client error row")


def classify(repeat, timeout_as_kept=False):
    if repeat["first"]["result"] != "success":
        return "first_failed"
    windows = repeat["windows"]
    if windows["idle_timed_disconnects"] > 0:
        return "closed_while_idle"
    if windows["resume_idle_timed_disconnects"] > 0:
        return "stale_on_resume"
    if windows["resume_timeout_reconnect"] is True and not timeout_as_kept:
        return "timed_out_on_resume"
    return "kept"


def summarize(classes):
    if "first_failed" in classes:
        return "first_failed"
    if all(c == "kept" for c in classes):
        return "kept"
    if all(c in ("closed_while_idle", "stale_on_resume", "timed_out_on_resume") for c in classes):
        return "not_kept"
    return "unequal"


def item_rule(s0, s1):
    undecided = {"H_keepalive": "undecided", "H_resume": "undecided"}
    if "first_failed" in (s0, s1):
        return ("untestable", "first_produce_failed", undecided)
    if "unequal" in (s0, s1):
        return (None, "unequal_classes", undecided)
    if s0 == "kept" and s1 == "not_kept":
        return (None, "inverse_effect", {"H_keepalive": "undecided", "H_resume": "recorded_only"})
    if s1 == "not_kept":
        return ("contradicted", "closed_with_keepalive", {"H_keepalive": "contradicted", "H_resume": "recorded_only"})
    if s0 == "not_kept":
        return ("confirmed", "keepalive_kept_connection", {"H_keepalive": "holds", "H_resume": "recorded_only"})
    return ("untestable", "no_idle_close_observed", {"H_keepalive": "undecided", "H_resume": "recorded_only"})


def check_g6(candidate):
    summaries = {}
    prior = {}
    for configuration in candidate["configurations"]:
        cid = configuration["configuration_id"]
        classes = []
        for repeat in configuration["repeats"]:
            expect(classify(repeat) == repeat["connection_class"], "G6", cid + " repeat " + str(repeat["repeat"]) + " class differs from recomputation")
            classes.append(repeat["connection_class"])
        expect(summarize(classes) == configuration["summary"], "G6", cid + " summary differs from recomputation")
        summaries[cid] = configuration["summary"]
        prior[cid] = summarize([classify(r, timeout_as_kept=True) for r in configuration["repeats"]])
        expect(classes.count("timed_out_on_resume") == 1, "G6", cid + " does not have exactly one timed_out_on_resume repeat")
    verdict, reason, statuses = item_rule(summaries["C0"], summaries["C1"])
    assessment = candidate["item_assessment"]
    expect((assessment["verdict"], assessment["reason"], assessment["hypotheses"]) == (verdict, reason, statuses), "G6", "assessment differs from recomputation")
    expect([prior["C0"], prior["C1"]] == ["unequal", "unequal"], "G6", "summaries under the prior class rule are not unequal")


def check_g7(inputs):
    status = parse(inputs, "pods/phase-status.json", "G7")
    batch = parse(inputs, "pods/idle-observation-batch.json", "G7")
    expect(status.get("phase") == "idle-observation" and status.get("run_id") == RUN_ID, "G7", "phase or run")
    expect(status.get("safety_stop") is False and status.get("not_selected") == SELECTION["not_selected"], "G7", "safety stop or not_selected")
    modes = {m.get("job"): m for m in status.get("modes", [])}
    for cid in CONFIGS:
        mode = modes.get(PODS[cid][0])
        expect(mode is not None, "G7", "no mode for " + cid)
        expect(mode.get("outcome") == "complete" and mode.get("collect") == 0, "G7", cid + " outcome or collect")
        expect(mode.get("item") == "A02" and mode.get("configuration") == cid and mode.get("role") == "loadgen", "G7", cid + " identity")
        expect(mode.get("capture_complete") is True, "G7", cid + " capture")
        expect(mode.get("capture_pod") == {"name": PODS[cid][1], "uid": PODS[cid][2]}, "G7", cid + " capture pod")
    judge = modes.get(PODS["judge"][0])
    expect(judge is not None, "G7", "no judge mode")
    expect(judge.get("mode") == "idle-observation-judge" and judge.get("outcome") == "complete" and judge.get("collect") == 0, "G7", "judge mode")
    entries = [e for e in status.get("candidates", []) if e.get("item") == "A02"]
    expect(entries == [{"item": "A02", "configuration": "judge", "accepted": True, "transport_valid": True, "candidate": "A02-idle-candidate.json", "judge_completion": "idle-judge-completion.json"}], "G7", "A02 candidate entry")
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
        if name in CONFIGS:
            log = inputs["pods/" + job + "/" + pod + ".containers.loadgen.log"].decode("utf-8", errors="strict")
            found = {}
            for line in log.split("\n"):
                match = re.match(r"idle_observation_client_log r([0-9]+) [0-9]+\|([A-Z]+)\|", line)
                if match:
                    key = (name, int(match.group(1)))
                    found[key] = found.get(key, 0) + 1
                    expect(match.group(2) == "REQTMOUT", "G8", name + " tagged client log level")
            expected = {key: count for key, count in TAGGED_CLIENT_LOGS.items() if key[0] == name}
            expect(found == expected, "G8", name + " repeat-tagged client log lines")


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

    expect(at(batch, 915).endswith("No changes. Your infrastructure matches the configuration."), "G9", "batch line 915")
    line = at(batch, 1409)
    expect("module.eventhubs.azurerm_eventhub_namespace.this[0" in line and "will be created" in line, "G9", "batch line 1409")
    expect(at(batch, 1412).endswith("+ capacity                                  = 1"), "G9", "batch line 1412")
    expect(at(batch, 1421).endswith('+ location                                  = "uksouth"'), "G9", "batch line 1421")
    expect(at(batch, 1427).endswith('+ sku                                       = "Premium"'), "G9", "batch line 1427")
    expect(at(batch, 3749) == "2026-11-03T09:13:44.6637054Z ephemeral_state_empty", "G9", "batch line 3749")
    expect(at(stop, 343) == "2026-11-03T09:14:47.1492635Z aks_stopped", "G9", "stop line 343")


def check_g10(inputs):
    summary = parse(inputs, "c5/summary.json", "G10")
    survivors = parse(inputs, "c5/survivors.json", "G10")
    expect(summary.get("commit") == C5_COMMIT and summary.get("tree") == C5_TREE and summary.get("run_id") == C5_RUN_ID and summary.get("run_attempt") == "1", "G10", "summary identity")
    expect(summary.get("measurement_complete") is True and summary.get("incomplete_reasons") == [], "G10", "measurement complete")
    expect(summary.get("break_passed") is True, "G10", "break passed")
    expect(summary.get("numerator") == COMBINED[7] and summary.get("denominator") == COMBINED[8], "G10", "summary score")
    expect(summary.get("rebalance_needed") == [], "G10", "rebalance_needed")
    shards = summary.get("shards")
    expect(isinstance(shards, dict) and list(shards) == SHARDS, "G10", "shard keys")
    for name in SHARDS:
        expect(isinstance(shards[name], dict) and shards[name].get("child_exit_code") == 0, "G10", "child_exit_code of " + name)
    targets = summary.get("targets")
    expect(isinstance(targets, dict) and list(targets) == [row[0] for row in TARGETS], "G10", "summary populations")
    for row in TARGETS:
        entry = targets[row[0]]
        expect(entry.get("target") == row[1] and entry.get("parts") == row[2], "G10", "target path or parts for " + row[0])
        counts = entry.get("status_counts")
        expect(isinstance(counts, dict) and list(counts) == list(STATUS_NAMES), "G10", "status count keys for " + row[0])
        expect([counts[name] for name in STATUS_NAMES] == list(row[3:10]), "G10", "status counts for " + row[0])
        expect(entry.get("numerator") == row[10] and entry.get("denominator") == row[11], "G10", "score for " + row[0])
    combined = summary.get("status_counts")
    expect(isinstance(combined, dict) and [combined.get(name) for name in STATUS_NAMES] == list(COMBINED[:7]), "G10", "combined status counts")
    config_path = "tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json"
    config = json.loads(git_show(config_path, C5_COMMIT, "G10"))["stryker-config"]
    expect(config["thresholds"]["break"] == 60, "G10", "break threshold")
    expect(config["mutate"] == [row[1] for row in TARGETS], "G10", "mutate list")
    exact_keys(survivors, ("survivors", "disposition_sample", "unreviewed_beyond_cap"), "G10", "survivors.json")
    isolated = {name: len(entry.get("isolated_mutants", [])) for name, entry in shards.items() if entry.get("isolated_mutants")}
    expect(isolated == ISOLATED_MUTANTS, "G10", "isolated mutants per shard")
    expect(sum(isolated.values()) == 42, "G10", "isolated mutants total")
    expect(len(survivors["survivors"]) == COMBINED[2], "G10", "survivor count")
    expect(all(item.get("status") == "Survived" for item in survivors["survivors"]), "G10", "survivor status")
    expect(survivors["disposition_sample"] == survivors["survivors"][:50], "G10", "survivor sample")
    expect(survivors["unreviewed_beyond_cap"] == COMBINED[2] - 50, "G10", "unreviewed_beyond_cap")
    for row in TARGETS:
        found = sum(1 for item in survivors["survivors"] if item.get("file") == row[1])
        expect(found == row[5], "G10", "survivor count for " + row[1])
    return summary


def check_g10a(source=SOURCE, commit=C5_COMMIT):
    """The campaign commit and the batch source differ in no verifier source or scenario."""
    quiet = subprocess.run(["git", "-C", str(ROOT), "diff", "--quiet", source, commit, "--", "src", "scenarios"], capture_output=True)
    expect(quiet.returncode == 0, "G10a", "src or scenarios differ between the batch source and the campaign commit")
    names = subprocess.run(["git", "-C", str(ROOT), "diff", "--name-only", source, commit], capture_output=True)
    expect(names.returncode == 0, "G10a", "git diff --name-only failed")
    changed = names.stdout.decode("utf-8").split("\n")
    expect(not any(n.endswith(".cs") or n.endswith(".csproj") for n in changed), "G10a", "a .cs or .csproj file differs")


def check_g11(sources, expected):
    for path, pin in SOURCES.items():
        committed = git_show(path)
        expect(sha256(committed) == pin, "G11", "committed " + path)
        if path in WORKING_TREE_SOURCES:
            expect(sources[path] == committed, "G11", "working tree differs for " + path)
    expect(sha256(expected) == SOURCES["scenarios/A02/expected.yaml"], "G11", "expected.yaml bytes differ from the pin")
    lines = expected.decode("utf-8").split("\n")
    for line in EXPECTED_LINES:
        expect(lines.count(line) == 1, "G11", "expected.yaml line missing or repeated: " + line[:60])


def quiet_ms(broker):
    if broker is None:
        return 0
    tx, rx = broker["tx_idle_us"], broker["rx_idle_us"]
    if tx == -1 and rx == -1:
        return 0
    micros = rx if tx == -1 else tx if rx == -1 else min(tx, rx)
    return micros // 1000


def repeat_events(reports):
    """Rows (report index, sequence, broker key, broker, earlier broker) where disconnects or req_timeouts rose."""
    rows = []
    for index in range(1, len(reports)):
        before = reports[index - 1]["brokers"]
        for key, broker in reports[index]["brokers"].items():
            earlier = before.get(key)
            if (broker["disconnects"] > (earlier["disconnects"] if earlier else 0)
                    or broker["req_timeouts"] > (earlier["req_timeouts"] if earlier else 0)):
                rows.append((index, reports[index]["sequence"], key, broker, earlier))
    return rows


def rose(reports, field):
    """Per broker key, the report indexes (from 1) at which `field` rose against the previous report."""
    result = {}
    for index in range(1, len(reports)):
        before = reports[index - 1]["brokers"]
        for key, broker in reports[index]["brokers"].items():
            earlier = before.get(key)
            if broker[field] > (earlier[field] if earlier else 0):
                result.setdefault(key, []).append(index)
    return result


def check_g12(candidate):
    maxima = {}
    events = []
    for configuration in candidate["configurations"]:
        cid = configuration["configuration_id"]
        maxima[cid] = []
        for repeat in configuration["repeats"]:
            label = cid + " repeat " + str(repeat["repeat"])
            reports = repeat["reports"]
            maxima[cid].append(max([quiet_ms(b) for report in reports for b in report["brokers"].values()] or [0]))
            rows = []
            timeout_total = 0
            for index in range(1, len(reports)):
                before = reports[index - 1]["brokers"]
                for key, broker in reports[index]["brokers"].items():
                    earlier = before.get(key)
                    timeout_total += max(0, broker["req_timeouts"] - (earlier["req_timeouts"] if earlier else 0))
            for index, sequence, key, broker, earlier in repeat_events(reports):
                disconnects = broker["disconnects"] - (earlier["disconnects"] if earlier else 0)
                timeouts = broker["req_timeouts"] - (earlier["req_timeouts"] if earlier else 0)
                connects = broker["connects"] - (earlier["connects"] if earlier else 0)
                rows.append((cid, repeat["repeat"], sequence, broker["node_id"], disconnects, quiet_ms(earlier), timeouts, connects))
            events.extend(rows)
            windows = repeat["windows"]
            timed = sum(r[4] for r in rows if r[5] >= IDLE_TIMED_MS)
            other = sum(r[4] for r in rows if r[5] < IDLE_TIMED_MS)
            expect(timed == windows["idle_timed_disconnects"] + windows["resume_idle_timed_disconnects"], "G12", label + " idle_timed disconnects")
            expect(other == windows["idle_other_cause_disconnects"] + windows["resume_other_cause_disconnects"], "G12", label + " other_cause disconnects")
            expect(timeout_total == windows["idle_request_timeouts"] + windows["resume_request_timeouts"], "G12", label + " request timeouts")
            timeouts_at, disconnects_at, connects_at = rose(reports, "req_timeouts"), rose(reports, "disconnects"), rose(reports, "connects")
            sequence_found = any(a <= b <= c
                                 for key in timeouts_at for a in timeouts_at[key]
                                 for b in disconnects_at.get(key, []) for c in connects_at.get(key, []))
            expect(sequence_found == windows["resume_timeout_reconnect"], "G12", label + " resume timeout sequence")
            expect(windows["idle_metadata_requests"] == 0, "G12", label + " idle Metadata requests")
    expect(maxima == REPEAT_MAX_QUIET, "G12", "largest quiet periods differ")
    expect(events == EVENTS, "G12", "event rows differ")


def read_sources():
    return {path: (ROOT / path).read_bytes() for path in WORKING_TREE_SOURCES}


def verify(inputs, pins=True, sources=None):
    if pins:
        check_g1(inputs)
    receipt = check_g2(inputs)
    candidate = parse(inputs, "cand/A02-idle-candidate.json", "G3")
    check_g3(candidate)
    check_g4(candidate)
    check_g5(candidate)
    check_g6(candidate)
    check_g7(inputs)
    check_g8(inputs)
    check_g9(inputs)
    summary = check_g10(inputs)
    check_g10a()
    survivors = parse(inputs, "c5/survivors.json", "G10")
    sources = sources if sources is not None else read_sources()
    check_g11(sources, sources["scenarios/A02/expected.yaml"])
    check_g12(candidate)
    return {"candidate": candidate, "receipt": receipt, "summary": summary, "survivors": survivors, "inputs": inputs}


def event_rows():
    return [{"configuration": r[0], "repeat": r[1], "report": r[2], "node_id": r[3], "disconnects_added": r[4],
             "quiet_before_ms": r[5], "request_timeouts_added": r[6], "connects_added": r[7]} for r in EVENTS]


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


def survivor_list(survivors):
    sample = {item["id"] for item in survivors["disposition_sample"]}
    return [{"id": int(item["id"]), "target": item["file"], "line": item["location"]["start"]["line"],
             "mutator": item["mutatorName"], "in_disposition_sample": item["id"] in sample}
            for item in survivors["survivors"]]


def render_json(ctx):
    candidate = ctx["candidate"]
    assessment = candidate["item_assessment"]
    document = {
        "publication": {
            "item": "A02", "proposed_verdict": assessment["verdict"], "reason": assessment["reason"],
            "hypotheses": assessment["hypotheses"],
            "verdict_authority": "issue 301 decision 6563540913 point 1, adopted on issue 320 by decision 6563546020",
            "workload": "synthetic", "adr012_counts": "not_applicable", "notes": ["k37_not_tested"], "metadata_timers_ms": 900000,
            "expected_outcome": {
                "expected": "not_kept in C1 as in C0, an inference from K-35",
                "observed_summaries": {"C0": "not_kept", "C1": "not_kept"}, "matches": True,
            },
            "rule_history": {
                "class": "timed_out_on_resume", "designed_after_run": 41534423570,
                "decision": "issue 301 decision 6596426514, adopted on issue 320 by decision 6596432641",
                "summaries_under_prior_class_rule": {"C0": "unequal", "C1": "unequal"},
            },
            "events": {"repeat_max_quiet_ms": REPEAT_MAX_QUIET, "rows": event_rows()},
            "run": {
                "workflow_run": RUN, "attempt": 1, "run_id": RUN_ID, "source_commit": SOURCE, "run_kind": "batch", "items": "A02,A04",
                "conclusion": "success", "created_at": "2026-11-03T08:17:22Z",
                "candidate_artifact": {"id": 13218410622, "name": "idle-observation-candidates-41602168787-1", "zip_sha256": "99b0f3e3ab24c97594ee775eadfc297c7885158390ee2925df5b0f8efb62ec63"},
                "pod_artifact": {"id": 13218221146, "name": "idle-observation-pods-41602168787-1", "zip_sha256": "2233339c5361c6dd2b23f4cb276aeab49976159155f69b8a435be1631b86b459"},
                "artifacts_expire_at": "2027-02-01T08:17:23Z", "batch_job_id": 125803257704, "stop_job_id": 125826936334,
                "batch_log_sha256": PINS["logs/batch.log"], "stop_log_sha256": PINS["logs/stop.log"],
            },
            "rules": {
                "scenario_yaml_sha256": SOURCES["scenarios/A02/scenario.yaml"],
                "expected_yaml_sha256": SOURCES["scenarios/A02/expected.yaml"],
                "plan": "issue 319 comments 6575747094 and 6579253743, Revision 6",
                "plan_check": "issue 319 comment 6579727682", "pull_request": 328,
                "merged_at": "2026-11-02T16:39:43Z", "merge_commit": "f0850d44086c4065cad76e64da6ec6f2d9cc50ce",
                "timers_plan": "issue 329 comment 6588394554, Revision 2", "timers_plan_check": "issue 329 comment 6588805136",
                "timers_pull_request": 330, "timers_merged_at": "2026-11-02T22:06:44Z",
                "timers_merge_commit": "9014b8effd4f8556d3446bcfbf8f951a52905bb2",
                "resume_timeout_plan": "issue 333 comment 6596783115, Revision 2", "resume_timeout_plan_check": "issue 333 comment 6597891329",
                "resume_timeout_pull_request": 336, "resume_timeout_merged_at": "2026-11-03T08:06:49Z", "resume_timeout_merge_commit": SOURCE,
            },
            "environment": {
                "region": "uksouth", "event_hubs_sku": "Premium", "pu_count": 1, "node_size": "Standard_D4ds_v5",
                "node_size_evidence": "inferred_from_pinned_configuration_and_no_change_cluster_plan",
                "managed_version": "2.16.0.0", "native_version": "2.16.0",
                "image_digests": {"loadgen": LOADGEN_DIGEST, "verifier": VERIFIER_DIGEST},
                "ephemeral_state_empty_at": "2026-11-03T09:13:44.6637054Z", "aks_stopped_at": "2026-11-03T09:14:47.1492635Z",
            },
            "jobs": job_rows(),
            "mutation_gate": {
                "workflow_run": C5_RUN, "attempt": 1,
                "summary_artifact": C5_ARTIFACT,
                "summary_json_sha256": PINS["c5/summary.json"], "survivors_json_sha256": PINS["c5/survivors.json"],
                "measured_commit": C5_COMMIT, "measured_tree": C5_TREE, "batch_source_commit": SOURCE,
                "source_equality": "git diff --quiet c878a9a 87327f5 -- src scenarios exits 0; no .cs or .csproj file differs",
                "isolated_mutants": 42, "shards": 18,
                "measurement_complete": True, "break_passed": True, "break_threshold": 60,
                "numerator": COMBINED[7], "denominator": COMBINED[8], "survivor_count": COMBINED[2], "disposition_sample_count": 50,
                "unreviewed_beyond_cap": COMBINED[2] - 50, "newly_exposed_survivors": 61, "dispositions": "none in this pull request",
                "survivors": survivor_list(ctx["survivors"]),
            },
        },
        "judge_receipt": ctx["receipt"],
        "candidate": candidate,
    }
    return json.dumps(document, indent=2, ensure_ascii=True).encode() + b"\n"


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
            lines.append(row([cid, "`" + KEEPALIVE[cid] + "`", "900000", cell(c["status"], True), cell(c["summary"], True), cell(c["capture_sha256"], True)]))
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
                lines.append(row([c["configuration_id"], str(r["repeat"])] + [cell(w.get(k)) for k in WINDOW_KEYS]
                                 + [str(len(r["client_errors"])), str(r["client_errors_dropped"])]))
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
    elif name == "events":
        for r in EVENTS:
            lines.append(row([r[0], str(r[1]), str(REPEAT_MAX_QUIET[r[0]][r[1] - 1]), str(r[2]), str(r[3]), str(r[4]), str(r[5]), str(r[6]), str(r[7])]))
    elif name == "jobs":
        for j in job_rows():
            lines.append(row([j["configuration"], j["stage"], "`" + j["job"] + "` (`" + j["account"] + "`)", j["container_started"],
                              j["container_finished"], "`" + j["pod_log_member"] + "`", "`" + j["pod_json_member"] + "`"]))
    elif name == "mutation-targets":
        for t in TARGETS:
            lines.append(row(["`" + t[1] + "`"] + [str(v) for v in t[2:10]] + [str(t[10]) + " / " + str(t[11])]))
        lines.append(row(["All seven targets", str(sum(t[2] for t in TARGETS))] + [str(v) for v in COMBINED[:7]]
                         + [str(COMBINED[7]) + " / " + str(COMBINED[8])]))
    elif name == "survivors":
        for s in survivor_list(ctx["survivors"]):
            lines.append(row([str(s["id"]), "`" + s["target"] + "`", str(s["line"]), s["mutator"], cell(s["in_disposition_sample"])]))
    return TABLE_HEADERS[name] + lines


def tokens(ctx):
    by_id = {c["configuration_id"]: c for c in ctx["candidate"]["configurations"]}
    values = {
        "C0_CLASSES": class_counts(by_id["C0"]), "C1_CLASSES": class_counts(by_id["C1"]),
        "DECISION": DECISION, "PRECEDENCE": PRECEDENCE,
    }
    for cid in CONFIGS:
        repeat = next(r for r in by_id[cid]["repeats"] if r["connection_class"] == "timed_out_on_resume")
        number = repeat["repeat"]
        event = next(e for e in EVENTS if e[0] == cid and e[1] == number)
        values["TO_REPEAT_" + cid] = str(number)
        values["TO_MAX_" + cid] = str(REPEAT_MAX_QUIET[cid][number - 1])
        values["TO_Q_" + cid] = str(event[5])
        values["TO_LAT_" + cid] = str(repeat["second"]["latency_ms"])
    closes = [e[5] for e in EVENTS if e[5] >= IDLE_TIMED_MS]
    latencies = [r["second"]["latency_ms"] for c in ctx["candidate"]["configurations"] for r in c["repeats"]
                 if r["connection_class"] == "closed_while_idle"]
    values.update({"CLOSE_MIN": str(min(closes)), "CLOSE_MAX": str(max(closes)),
                   "CW_LAT_MIN": str(min(latencies)), "CW_LAT_MAX": str(max(latencies))})
    return values


def fill(text, values):
    for key, value in values.items():
        text = text.replace("@@" + key + "@@", value)
    return text


def readme_row(ctx):
    return "| A02 | TCP keepalive configuration | " + fill(OBSERVED, tokens(ctx)) + " | contradicted | [Report](results/A02/2026-11-03.md) |"


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
        path = ROOT / "results" / "A02" / name
        expect(path.is_file() and path.read_bytes() == data, "G13", "results/A02/" + name + " differs from the rendered bytes")
    lines = (ROOT / "README.md").read_text(encoding="utf-8").split("\n")
    expect(len(lines) > 13 and lines[13] == readme_row(ctx), "G13", "README.md line 14 differs")


def edit_json(inputs, key, change):
    value = strict_json(inputs[key])
    change(value)
    inputs[key] = json.dumps(value, separators=(",", ":")).encode()


def refresh_receipt(inputs):
    def change(receipt):
        receipt["candidates"][0]["candidate_sha256"] = sha256(inputs["cand/A02-idle-candidate.json"])
        receipt["candidates"][0]["reason"] = strict_json(inputs["cand/A02-idle-candidate.json"])["item_assessment"]["reason"]
    edit_json(inputs, "cand/idle-judge-completion.json", change)


def candidate_edit(change):
    return lambda i: edit_json(i, "cand/A02-idle-candidate.json", change)


def self_test(inputs, sources):
    verify(inputs, pins=True, sources=sources)
    cases = []

    def case(name, code, mutate, pins=False, refresh=False):
        cases.append((name, code, mutate, pins, refresh))

    def append_newline(i):
        i["cand/A02-idle-candidate.json"] += b"\n"

    def add_extra(i):
        i["cand-listing"] = json.dumps(sorted(json.loads(i["cand-listing"]) + ["extra.json"])).encode()

    def break_hash(receipt):
        entry = receipt["candidates"][0]
        entry["candidate_sha256"] = entry["candidate_sha256"][:-1] + ("0" if entry["candidate_sha256"][-1] != "0" else "1")

    def set_quiet(c):
        broker = next(iter(c["configurations"][0]["repeats"][1]["reports"][0]["brokers"].values()))
        broker["tx_idle_us"] = broker["rx_idle_us"] = 999999999

    def drop_timeout(c):
        reports = c["configurations"][1]["repeats"][2]["reports"]
        report32 = next(r for r in reports if r["sequence"] == 32)
        report33 = next(r for r in reports if r["sequence"] == 33)
        for key, broker in report33["brokers"].items():
            if broker["node_id"] == 8:
                broker["req_timeouts"] = report32["brokers"][key]["req_timeouts"]

    def restart(i):
        edit_json(i, "pods/" + PODS["C1"][0] + "/pods.json", lambda p: p["items"][0]["status"]["containerStatuses"][0].update(restartCount=1))

    def gap(i):
        edit_json(i, "pods/" + PODS["judge"][0] + "/collection-status.json", lambda s: s.update(gaps=["x"]))

    def tagged_log(i):
        key = "pods/" + PODS["C0"][0] + "/" + PODS["C0"][1] + ".containers.loadgen.log"
        i[key] = i[key].replace(b"idle_observation_client_log r1 ", b"idle_observation_client_log r2 ", 1)

    def teardown(i):
        lines = i["logs/batch.log"].decode("utf-8").split("\n")
        lines[3748] = "x"
        i["logs/batch.log"] = "\n".join(lines).encode()

    def drop_survivor(s):
        s["survivors"].pop()

    case("G1-append", "G1", append_newline, pins=True)
    case("G1-extra", "G1", add_extra, pins=True)
    case("G2-exit", "G2", lambda i: edit_json(i, "cand/idle-judge-completion.json", lambda r: r.update(handler_exit_code=1)))
    case("G2-hash", "G2", lambda i: edit_json(i, "cand/idle-judge-completion.json", break_hash))
    case("G3-notes", "G3", candidate_edit(lambda c: c.update(notes=[])), refresh=True)
    case("G3-kind", "G3", candidate_edit(lambda c: c.update(run_kind="smoke")), refresh=True)
    case("G4-word", "G4", candidate_edit(lambda c: c["item_assessment"].update(verdict="confirmed")), refresh=True)
    case("G5-status", "G5", candidate_edit(lambda c: c["configurations"][1]["repeats"][2].update(status="incomplete")), refresh=True)
    case("G5-client", "G5", candidate_edit(lambda c: c["configurations"][0]["repeats"][0].update(client_id="x-r1")), refresh=True)
    case("G5-window", "G5", candidate_edit(lambda c: c["configurations"][0]["repeats"][0]["windows"].pop("resume_timeout_reconnect")), refresh=True)
    case("G6-class", "G6", candidate_edit(lambda c: c["configurations"][0]["repeats"][0].update(connection_class="kept")), refresh=True)
    case("G6-hypothesis", "G6", candidate_edit(lambda c: c["item_assessment"]["hypotheses"].update(H_resume="undecided")), refresh=True)
    case("G7-stop", "G7", lambda i: edit_json(i, "pods/phase-status.json", lambda s: s.update(safety_stop=True)))
    case("G7-selected", "G7", lambda i: edit_json(i, "pods/phase-status.json", lambda s: s.update(not_selected=[])))
    case("G7-evidence", "G7", lambda i: edit_json(i, "pods/idle-observation-batch.json", lambda b: b.update(evidence_passed=False)))
    case("G8-restart", "G8", restart)
    case("G8-gap", "G8", gap)
    case("G8-tagged-log", "G8", tagged_log)
    case("G9-teardown", "G9", teardown)
    case("G10-break", "G10", lambda i: edit_json(i, "c5/summary.json", lambda s: s.update(break_passed=False)))
    case("G10-commit", "G10", lambda i: edit_json(i, "c5/summary.json", lambda s: s.update(commit="f0850d44086c4065cad76e64da6ec6f2d9cc50ce")))
    case("G10-isolated", "G10", lambda i: edit_json(i, "c5/summary.json", lambda s: s["shards"]["workload"]["isolated_mutants"].pop()))
    case("G10-survivor", "G10", lambda i: edit_json(i, "c5/survivors.json", drop_survivor))
    case("G10-cap", "G10", lambda i: edit_json(i, "c5/survivors.json", lambda s: s.update(unreviewed_beyond_cap=0)))
    case("G12-max", "G12", candidate_edit(set_quiet), refresh=True)
    case("G12-timeout", "G12", candidate_edit(drop_timeout), refresh=True)

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
    try:
        check_g11(sources, sources["scenarios/A02/expected.yaml"] + b"x")
        print("FAIL G11-expected: no check failed", file=sys.stderr)
        return 1
    except CheckFailure as error:
        if error.code != "G11":
            print("FAIL G11-expected: expected G11, got " + error.code, file=sys.stderr)
            return 1
    print("PASS G11-expected")
    try:
        check_g10a(SOURCE, "f0850d44086c4065cad76e64da6ec6f2d9cc50ce")
        print("FAIL G10a-source: no check failed", file=sys.stderr)
        return 1
    except CheckFailure as error:
        if error.code != "G10a":
            print("FAIL G10a-source: expected G10a, got " + error.code, file=sys.stderr)
            return 1
    print("PASS G10a-source")
    return 0


def main(argv):
    parser = argparse.ArgumentParser(description="Assemble the A02 publication offline.")
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
        out = ROOT / "results" / "A02"
        out.mkdir(parents=True, exist_ok=True)
        (out / "2026-11-03.json").write_bytes(json_bytes)
        (out / "2026-11-03.md").write_bytes(md_bytes)
        return 0
    except CheckFailure as error:
        print("FAIL " + error.code + ": " + error.message, file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

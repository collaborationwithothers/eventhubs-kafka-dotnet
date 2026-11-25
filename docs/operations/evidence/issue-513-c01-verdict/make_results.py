#!/usr/bin/env python3
"""Writes the C01 evidence pack and verdict inputs for batch run 45124695288.

Run it from anywhere with `python3 -I`. The inputs are the artifacts and job logs
of the run and the mutation summary of the accepted C5 mutation run, downloaded to one
directory outside the repository (INPUT_FILES names the files that directory must hold).
The mutation inputs are named on the command line, because the mutation run is not part of
the batch run:

  --mutation-run ID --mutation-artifact-id ID
  --mutation-zip-sha256 HASH --mutation-summary-sha256 HASH

  make_results.py --inputs DIR <mutation args>               check everything, then write
                                             results/C01/2026-11-23.json and .md
                                             and README.md line 30, and print the row
  make_results.py --inputs DIR <mutation args> --check       re-render and require byte
                                             equality with the committed files
  make_results.py --inputs DIR <mutation args> --self-test   run the real inputs, then each
                                             single-change case; each must fail at its named check

Checks run in this order: G1 (hashes), G11 (the expected-outcome edit is additive), G2 to G6
and G10 (the inputs), then rendering, G8 and G9 on the rendered files, and G7 (byte equality
with the committed files) only with --check. A failure prints `FAIL G<n>: <reason>` and
exits 1. The generator reads no network and holds no secret. It reads the expected outcome
the run was judged against with `git show`.
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

import yaml

ROOT = pathlib.Path(__file__).resolve().parents[4]
REPO = "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet"
RUN_ID_NUM = 45124695288
RUN_URL = f"{REPO}/actions/runs/{RUN_ID_NUM}"
RUN_ID = "c01-45124695288-1"
HEAD = "2d370de00059df30506154c1fd504d0f81d237bc"
BASE = "3e7163dc641e724437025d917032fd40419e6c4d"
JSON_PATH = "results/C01/2026-11-23.json"
MD_PATH = "results/C01/2026-11-23.md"
README_LINE = 30
ARTIFACT_EXPIRY = "2027-02-21T06:31:39Z"

EXPECTED_PATH = "scenarios/C01/expected.yaml"
JUDGED_SHA = "7fa82b42f1d09d2ac9e2fa6c204dbdce18b80e8813bcff89c0a6f5249c6dd2fb"
EDITED_SHA = "17ced6efa4af8ebb1d8f656fe7dd62fcadc5bcd0629c256840562d8b5c122deb"
SCENARIO_PATH = "scenarios/C01/scenario.yaml"
SCENARIO_SHA = "555cb0be7f37c6e5d6a6e94f899875f0616fcafca49be34fab4ad70fda59efd2"
CONFIGS = ("C0", "C1")
REPEATS = [1, 2, 3, 4, 5]
WINDOWS = {"C0": (27000000, 45000000), "C1": (42000000, 60000000)}
SESSION_TIMEOUT_S = {"C0": 30, "C1": 45}

# Input files: key -> (file name in the inputs directory, SHA-256). The mutation ZIP's hash is an argument.
INPUT_FILES = {
    "c0cand": ("c01-C0-candidate.zip", "bd388eac5dc682ca9f3d0a73d94847192aff6cbfef6d5cc120c70b84aac15fb3"),
    "c1cand": ("c01-C1-candidate.zip", "52105da54df8e4e95415c02eaf925163980e70479d5d35eaf48c4daff5fbb596"),
    "c0arch": ("c01-C0-archive.zip", "be25c6ac639f362a5716575f9becab797b66ff927fba25ad15354aedf6b77772"),
    "c1arch": ("c01-C1-archive.zip", "dac66b1feb1f5107e29dd2aec2e681db11b8869f85854e49c5fe040fd233a6ac"),
    "c0pods": ("c01-C0-pods.zip", "e49f1a3f2088318fe1ea88acd70f38ac46fce463c335a1b5c9c2dbe2fefd7ad5"),
    "c1pods": ("c01-C1-pods.zip", "aff647840c189209f12e6bdd0edfdba00c7a87a18e8da6833ed19ac92b19ba0a"),
    "c0log": ("probe.log", "655d77582f398d448fedd8a33c1e96da87f4e3ccce8c8aa5dbc60482a914fffe"),
    "c1log": ("full-C3.log", "ba2481fa746610661a8d94ec2d91a818bd9e39fbf3a2bc1c24af79efd0b92439"),
    "c5": ("c5-summary.zip", None),
}
ZIP_KEYS = ("c0cand", "c1cand", "c0arch", "c1arch", "c0pods", "c1pods", "c5")
MEMBER_PINS = {
    ("c0cand", "candidate.json"): "0448245936d7d27756b17088a81c17c208d8349451ac5a262202e9649d37929f",
    ("c1cand", "candidate.json"): "779a2625b0f7f817d6caa3ba1a62a6aace909be0263996af314e1979b98e149d",
    ("c0arch", "archive-receipt.json"): "df75ed9da0e8bdd11001485f0ab09badf3642cfdc66ba57f971bd38246632308",
    ("c0arch", "archive-manifest.json"): "0d7cd201bc7e9d1cbb18db622b75e16c1c4084a38f08c91e5b710a46be281e94",
    ("c1arch", "archive-receipt.json"): "0a5e9d3577323f06f8a6969ef15cd45f0d7bb9acc0619bb5b77b65599a6802df",
    ("c1arch", "archive-manifest.json"): "eb2d4f785a3e89ac5b2993e023177ffc934ffabcec49649b7a6d01de86ab8573",
    ("c0pods", "phase-status.json"): "6741510c7377cb3bc524e766bab8fa4474c3c132f4183381d512b163a4c86df7",
    ("c1pods", "phase-status.json"): "605647a1a8c64c2257867f324e3a98c6a825d5cdfd2e071d92bb7b86d8381039",
    ("c5", "summary.json"): None,
}
ARTIFACTS = {
    "C0": [("candidate", 14867803464, "c01-C0-candidate-45124695288-1", "c0cand"),
           ("archive", 14870530520, "c01-C0-archive-45124695288-1", "c0arch"),
           ("pods", 14868618067, "c01-C0-pods-45124695288-1", "c0pods")],
    "C1": [("candidate", 14895402374, "c01-C1-candidate-45124695288-1", "c1cand"),
           ("archive", 14896405743, "c01-C1-archive-45124695288-1", "c1arch"),
           ("pods", 14895726646, "c01-C1-pods-45124695288-1", "c1pods")],
}
LOGS = {"C0": (137529776430, "probe", "c0log"), "C1": (138067157139, "full-C3", "c1log")}
# Log line numbers of the Terraform plan lines (sku, capacity, location), as the plan cites them.
LOG_LINES = {"C0": (1605, 1590, 1599), "C1": (1411, 1396, 1405)}
JOBS = ("consumer-subject", "consumer-witness", "start-line", "loadgen", "verify")

# Pinned outcomes of the rules of the expected outcome the run was judged against (plan section 4.2),
# in the order G3 compares them.
PINNED = [
    ("configurations.C0.H1_removal_follows_30000", "holds"),
    ("configurations.C0.early_removal", None),
    ("configurations.C0.H3_k57_latest_reset_skips_messages", "holds"),
    ("configurations.C0.reassignment", [31794219, 32994458, 35956669, 38562494, 37192516]),
    ("configurations.C1.H2_removal_follows_45000", "contradicted"),
    ("configurations.C1.early_removal", None),
    ("configurations.C1.H3_k57_latest_reset_skips_messages", "holds"),
    ("configurations.C1.reassignment", [63539433, 68186056, 62547934, 68071749, 61342653]),
    ("verdict", "contradicted"),
]

# The eight items appended to verdict.text_must_state by decision 7128051335 point 1 (plan section 4.10),
# as the 42 inserted lines of scenarios/C01/expected.yaml.
APPENDED_YAML = """\
    - >-
      Decision 7126257034 point 1, in plain words before any verifier term:
      the scenario as a production event. A consumer in a brand-new group is
      killed before it has ever committed an offset, and another member takes
      over its partitions, with the baseline settings (auto.offset.reset
      latest, auto-commit every 5 s) and messages flowing throughout.
    - >-
      Decision 7126257034 point 2, step by step: the dead member's partitions
      are unread until the session timeout expires; messages keep arriving;
      the survivor finds no committed offset, falls back to latest and starts
      at the end; what arrived between the crash and the takeover is skipped;
      the survivor's commits then pass the skipped messages; no error is
      raised. Each step the run did not measure is labelled an inference with
      its source.
    - >-
      Decision 7126257034 point 3: what was measured, per configuration, the
      takeover times and the messages skipped per repeat, from the published
      run.
    - >-
      Decision 7126257034 point 4: contradicted is against the 15 s rebalance
      allowance in the upper bound, a policy value. The run does not show that
      30000 is the right value or that 45000 is wrong; it measured failover
      time only and did not test the cost of a shorter timeout (a live member
      that stalls being removed by mistake). The text recommends no value.
    - >-
      Decision 7126257034 point 5: the skip count grows with takeover time, at
      about the rate the published run shows, stated as an inference from the
      numbers, not a measured rate.
    - >-
      Decision 7126257034 point 6: a shorter session timeout shrinks the loss
      but does not remove it.
    - >-
      Decision 7126257034 point 7: remedies such as auto.offset.reset earliest
      or error were not tested here; if the text names them, it says so and
      cites official documentation for their behaviour. Storing offsets after
      processing does not address this case, because nothing was committed.
    - >-
      Decision 7126257034 point 8: when it happens in production (a new
      group's first deployment crashing before its first commit, committed
      offsets that expired, a partition never committed to), and that a group
      that has been committing normally resumes from its last commit instead,
      the C10 case.
"""
APPENDED_ITEMS = yaml.safe_load("items:\n" + APPENDED_YAML)["items"]
assert len(APPENDED_ITEMS) == 8 and len(APPENDED_YAML.splitlines()) == 42

# Published text of the report, as the plan gives it (section 4.4). Fields in braces are filled from the data.

TITLE = (
    '# C01: a consumer killed before its first commit was replaced {c0t} s after the kill at session timeout '
    '30000 ms and {c1t} s at 45000 ms; in every repeat some messages were marked done without being processed'
)

SUMMARY = (
    'In run 45124695288 on Event Hubs Premium, a consumer in a new two-member group was killed at its first '
    'message, before processing it, and the other member took over its partitions {c0t} s after the kill with '
    '`session.timeout.ms` 30000 and {c1t} s with 45000, in five repeats each. In every repeat, messages were '
    'marked done without ever being processed: {c0s} per repeat at 30000 and {c1s} at 45000. The proposed '
    'verdict is `contradicted` because takeover at 45000 exceeded the 15 s rebalance allowance, a policy '
    'value, in all five repeats; this does not show that either value is right or wrong, and the workload is '
    'synthetic.'
)

VERDICT_LINE = 'Proposed verdict: `contradicted`.'

WHAT_TESTED = (
    'Each repeat ran two consumers in one group: a subject that was killed and a witness that ran the '
    "baseline consumer and received the subject's partitions. The load generator offered 4 synthetic messages "
    'per second for 150 s, 600 per repeat, in five repeats per configuration. C0 runs the baseline, with '
    '`session.timeout.ms` 30000. C1 sets `session.timeout.ms` to 45000 on the subject and changes nothing '
    'else; the witness runs the baseline in both configurations. K-57, a consumer killed before its first '
    "commit with `auto.offset.reset` at its baseline, is tested here and not under C10, because C10's two "
    'phases compare offset-store modes.'
)

RESULTS_SENTENCE = (
    '"Committed, never processed" is the verifier\'s name for a message marked done that nobody processed. '
    '"Takeover" is the verifier\'s `reassignment`: the time from the subject\'s last record row, its kill '
    'intent, to the witness\'s first assignment that includes every partition the subject held. "Skipped per '
    'second of takeover" is computed here, not measured.'
)

COMMIT_EVIDENCE = (
    "After each repeat the verifier read the group's committed offsets from Event Hubs without joining the "
    'group (`offset_read`, outcome `success` in all ten repeats). A repeat counts only when every committed '
    "offset it read matches an offset the members' own commit records report (otherwise it is invalid), and "
    'only when, on every partition that received messages during the run, the committed offset reached the '
    'run\'s end line (otherwise it is incomplete). So "committed" here is observed from the broker, not only '
    'reported by the consumers. Every delivered message was processed once or never: none was processed twice.'
)

BROKER_SENTENCE = (
    "Broker connection counts are the load generator producer's only, and are lower bounds, as each repeat's "
    '`broker_count.lower_bound` records.'
)

SELF_KILL_SENTENCE = (
    'The subject has no native-monitoring evidence; the archive records its missing index as '
    '`configured_self_kill` in each of the ten repeats.'
)

NODE_SENTENCE = (
    'Both consumers of every repeat ran on the same `workload` node; every subject exited with code 137 '
    '(SIGKILL) and every witness exited 0.'
)

MUTATION_SENTENCE = (
    "That commit is on `main` and includes `2d370de00059df30506154c1fd504d0f81d237bc`, the run's head "
    '(docs/agents/factory.md "Evidence pack").'
)

README_ROW = (
    '| C01 | Session timeout configuration | A consumer in a new group was killed before its first commit, '
    "and the other member took over its partitions {c0t} s later with `session.timeout.ms` 30000 (Microsoft's "
    'recommendation) and {c1t} s later with 45000 (the librdkafka default). In every repeat, messages were '
    'marked done without being processed: {c0s} per repeat at 30000 and {c1s} at 45000, about 2 for each '
    'second of takeover (an inference from the numbers). Inferred from the librdkafka documentation: the '
    'member that takes over finds no committed offset, and `auto.offset.reset` `latest` starts it at the end '
    'of each partition without an error. "Contradicted" is against a 15 s rebalance allowance, a policy '
    'value; the run measured failover time only and recommends no session timeout. Synthetic workload. | '
    'contradicted | [Report](results/C01/2026-11-23.md) |'
)

PLAIN_WORDS = [
    (
        '**The production event.** A consumer in a brand-new group is killed before it has ever committed an '
        'offset, and the other member of the group takes over its partitions. Both consumers run this '
        "repository's baseline settings: `auto.offset.reset` `latest`, offsets committed automatically every 5000 "
        'ms, and each offset stored only after its message is processed. Messages keep arriving throughout. '
        '"Marked done" means the group\'s committed offset for a partition has moved past a message, so no member '
        "will read it again. The first setting (C0) uses `session.timeout.ms` 30000, Microsoft's recommendation "
        'for Event Hubs; the second (C1) gives only the killed consumer 45000, the librdkafka default, and the '
        'member that takes over keeps 30000.'
    ),
    (
        '**What happens, step by step.** The run measured the takeover time and the counts below; the steps that '
        'connect them come from the librdkafka documentation and source listed under Sources, and are an '
        'inference, not a measurement. The killed member sends no more heartbeats, so its partitions go unread '
        "until the broker's session timeout removes it from the group. Messages keep arriving on those "
        'partitions. The member that takes over finds no committed offset for them, because the killed member '
        'never committed one, so `auto.offset.reset` `latest` starts it at the end of each partition. Everything '
        'that arrived between the crash and the takeover is skipped. Once the survivor commits newer offsets, the '
        'group treats the skipped messages as done. No error is raised: with `latest` librdkafka resets on its '
        "own, and only the setting `error` raises an error; the consumers' logs are empty, so this was not "
        "observed in the run. The killed member committed nothing; the survivor's commits are what pass the "
        'skipped messages.'
    ),
    (
        "**What was measured.** With session timeout 30000 ms the other member held the killed member's "
        'partitions {c0t} s after the kill, and {c0s} messages per repeat were marked done without being '
        'processed. With 45000 ms on the killed member, the takeover came {c1t} s after the kill, and {c1s} '
        "messages per repeat were skipped. After each repeat the verifier read the group's committed offsets from "
        "Event Hubs and found them at the run's end line on every partition that received messages. Each repeat "
        'sent 600 synthetic messages over 150 s, and all five repeats of each setting were valid.'
    ),
    (
        '**What the verdict means and does not mean.** The prediction for each setting allowed 15 s of rebalance '
        'time beyond the session timeout; that 15 s is a policy value chosen before the run, not a measurement. '
        'At 30000 ms every takeover fell inside its window, so that prediction held. At 45000 ms every takeover '
        'was later than 60 s, the end of its window, so that prediction is contradicted, and with it the verdict. '
        'The run does not show that 30000 ms is the right value or that 45000 ms is wrong. It measured failover '
        'time only. It did not test the cost of a shorter timeout: a live member that stalls being removed from '
        'the group by mistake. This result describes failover time and recommends no value.'
    ),
    (
        '**Time beyond the session timeout.** The takeover came {c0x} s after the session timeout at 30000 ms, '
        'and {c1x} s after it at 45000 ms. The run does not show why the extra time is larger at 45000 ms.'
    ),
    (
        '**More takeover time, more skipped messages.** In every repeat, the skipped messages divided by the '
        'takeover time in seconds came to {allr} ({c0r} at 30000 ms, {c1r} at 45000 ms), so about 2 messages were '
        'skipped for each second of takeover. This is an inference from the numbers, not a measured rate.'
    ),
    (
        '**A shorter timeout shrinks the loss but does not remove it.** Messages were skipped at both tested '
        'values, and fewer at 30000 ms. That every session timeout leaves some loss in this case is an inference: '
        'the survivor cannot take over before the broker removes the killed member, and messages that arrive '
        'until then are skipped.'
    ),
    (
        '**Remedies, not tested here.** With `auto.offset.reset` `earliest`, librdkafka resets a partition with '
        'no committed offset "to the smallest offset"; with `error`, it triggers the error '
        '`ERR__AUTO_OFFSET_RESET`, which the application receives when it consumes messages (librdkafka v2.16.0 '
        "`CONFIGURATION.md` line 206). Positioning the consumer after that error is the application's choice, an "
        'inference not tested here. Neither was tested here. Inference, not tested: with `earliest` the survivor '
        'reads every message still retained on those partitions again, so processing must tolerate repeats. '
        'Storing offsets only after processing, which both consumers did here, does not prevent this case, '
        'because nothing was ever committed.'
    ),
    (
        "**When this happens in production.** A new group's first deployment crashes before its first commit. A "
        'group\'s committed offsets have expired: Microsoft lists "Consumer offsets expired beyond Event Hubs '
        'retention period" as a likely cause of an unexpected offset reset, and gives no duration. A partition '
        'was never committed to. A group that has been committing normally resumes from its last commit instead; '
        'that case is tested under C10, offset storage and crash outcomes ([scenario '
        'definition](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/scenarios/C10/scenario.yaml)).'
    ),
]

HYPOTHESES = [
    (
        '- H1, removal follows 30000 (C0): holds. All five takeovers, {c0t} s, are inside the window from 27 s up '
        'to but not including 45 s.'
    ),
    (
        '- H2, removal follows 45000 (C1): contradicted. All five takeovers, {c1t} s, are at or after 60 s, the '
        'end of the window from 42 s up to but not including 60 s.'
    ),
    (
        '- H3, K-57, a latest reset skips messages, published on its own line and not part of the verdict: holds '
        'in C0 and C1. Every repeat had at least one message committed but never processed ({c0s} in C0, {c1s} in '
        'C1).'
    ),
    (
        '- Early removal, a finding on its own line: none. No takeover in either configuration came before the '
        'session timeout minus the heartbeat interval (27 s in C0, 42 s in C1).'
    ),
    '- Takeover times, recorded only and published on their own line: as in Results by repeat.',
]

NOT_TESTED = [
    '- `auto.offset.reset` `earliest` or `error` in this case: not tested.',
    '- The cost of a shorter session timeout, a live member that stalls being removed by mistake: not tested.',
    '- Session timeouts other than 30000 and 45000 ms: not tested.',
]

SOURCES = [
    (
        '- Run '
        '[45124695288](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45124695288), '
        'its artifacts and job logs, as listed above and in the JSON `inputs`.'
    ),
    (
        '- Scenario definition and expected outcome as approved: '
        '[scenarios/C01/scenario.yaml](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/2d370de00059df30506154c1fd504d0f81d237bc/scenarios/C01/scenario.yaml) '
        '(SHA-256 `555cb0be7f37c6e5d6a6e94f899875f0616fcafca49be34fab4ad70fda59efd2`) and '
        '[scenarios/C01/expected.yaml](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/2d370de00059df30506154c1fd504d0f81d237bc/scenarios/C01/expected.yaml) '
        '(SHA-256 `7fa82b42f1d09d2ac9e2fa6c204dbdce18b80e8813bcff89c0a6f5249c6dd2fb`), both at '
        '`2d370de00059df30506154c1fd504d0f81d237bc`, as the run was judged. The expected outcome as published '
        'adds eight `verdict.text_must_state` items by [decision '
        '7128051335](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7128051335) '
        'and changes no rule (SHA-256 `17ced6efa4af8ebb1d8f656fe7dd62fcadc5bcd0629c256840562d8b5c122deb`).'
    ),
    (
        '- Publication decision: '
        '[7126257034](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7126257034) '
        '(the plain-words points).'
    ),
    (
        '- librdkafka v2.16.0 '
        '[`CONFIGURATION.md`](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md) lines '
        '131, 132, 138, 139, 140 and 206, and '
        '[`src/rdkafka_assignment.c`](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_assignment.c) '
        'lines 214 to 220 and 631 to 635.'
    ),
    (
        '- Microsoft, [Apache Kafka client configurations for Azure Event '
        'Hubs](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations), "librdkafka '
        'configuration properties", consumer-only table (`session.timeout.ms` default 45000, recommended 30000), '
        'and "Troubleshooting common issues", row "Unexpected offset reset".'
    ),
    (
        '- This repository at `3e7163dc641e724437025d917032fd40419e6c4d`: '
        '[`BaselineSettingsCatalog.cs`](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs) '
        'lines 149, 151, 179, 181, 183 and 185; '
        '[`ScenarioConsumerProcessingPlanSource.cs`](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/src/Parchwell.Enrichment/ConsumerProcessing/ScenarioConsumerProcessingPlanSource.cs) '
        'line 11; '
        '[`ConsumerJudgement.cs`](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs) '
        'lines 63 to 74, 90 to 102 and 169 to 175; '
        '[`ConsumerCommittedOffsetReader.cs`](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/src/Parchwell.Verifier/VerifyRun/Consumer/Offsets/ConsumerCommittedOffsetReader.cs) '
        'line 11; '
        '[`c01-consumer.yaml`](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/scenarios/profiles/c01-consumer.yaml) '
        'line 20; '
        '[`aks.tf`](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/infra/cluster/aks.tf) '
        'lines 35, 44 and 51; '
        '[docs/blueprint.md](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/docs/blueprint.md) '
        'lines 756 (ADR-022, store after the processed row) and 1276 (K-57 under C01).'
    ),
    (
        '- C5 mutation run '
        '[<run>](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/<run>) and its '
        '`summary.json`, as in Mutation acceptance.'
    ),
    (
        '- Generator and receipt: '
        '[make_results.py](../../docs/operations/evidence/issue-513-c01-verdict/make_results.py) and '
        '[receipt.md](../../docs/operations/evidence/issue-513-c01-verdict/receipt.md).'
    ),
]


BLOB = f"{REPO}/blob/{BASE}/"
BLOB_HEAD = f"{REPO}/blob/{HEAD}/"
VERIFIER_TERMS = ("committed_never_processed", "reassignment", "offset_read", "complete_valid", "H1")


class Fail(Exception):
    def __init__(self, code, message):
        super().__init__(f"FAIL {code}: {message}")
        self.code = code
        self.text = message


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


def git(*args):
    result = subprocess.run(["git", "-C", str(ROOT), *args], capture_output=True)
    if result.returncode != 0:
        fail("G1", "git " + " ".join(args) + " failed: " + result.stderr.decode("utf-8", "replace").strip())
    return result.stdout


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


def new_context(raw, cfg):
    ctx = Ctx()
    ctx.raw = raw
    ctx.cfg = cfg
    ctx.members = {key: open_zip(raw[key], key) for key in ZIP_KEYS}
    ctx.judged = git("show", f"{HEAD}:{EXPECTED_PATH}")
    ctx.working_expected = (ROOT / EXPECTED_PATH).read_bytes()
    ctx.working_scenario = (ROOT / SCENARIO_PATH).read_bytes()
    return ctx


def zip_pin(ctx, key):
    return ctx.cfg["zip_sha"] if key == "c5" else INPUT_FILES[key][1]


def member_pin(ctx, key, member):
    return ctx.cfg["summary_sha"] if key == "c5" else MEMBER_PINS[(key, member)]


# ----------------------------------------------------------------------------------------------
# Checks


def check_g1(ctx):
    for key, (name, _pin) in INPUT_FILES.items():
        expect(sha256(ctx.raw[key]) == zip_pin(ctx, key), "G1", f"{name} differs from its pinned SHA-256")
    for (key, member) in MEMBER_PINS:
        expect(member in ctx.members[key], "G1", f"{key} has no member {member}")
        expect(sha256(ctx.members[key][member]) == member_pin(ctx, key, member), "G1", f"member {member} of {key} differs from its pin")
    expect(sha256(ctx.judged) == JUDGED_SHA, "G1", f"{EXPECTED_PATH} at {HEAD} differs from its pin")
    expect(sha256(ctx.working_expected) == EDITED_SHA, "G1", f"{EXPECTED_PATH} in the working tree differs from its pin")
    expect(sha256(ctx.working_scenario) == SCENARIO_SHA, "G1", f"{SCENARIO_PATH} in the working tree differs from its pin")


def check_g11(ctx):
    """The edited expected outcome equals the judged one, except for eight appended text_must_state items."""
    try:
        judged = yaml.safe_load(ctx.judged.decode("utf-8"))
        edited = yaml.safe_load(ctx.working_expected.decode("utf-8"))
    except (yaml.YAMLError, UnicodeDecodeError) as error:
        fail("G11", f"expected.yaml does not parse: {error}")
    j_items = judged["verdict"]["text_must_state"]
    e_items = edited["verdict"]["text_must_state"]
    expect(len(j_items) == 5, "G11", "the judged text_must_state does not have five items")
    expect(e_items[:5] == j_items, "G11", "the first five text_must_state items differ from the judged file")
    expect(e_items[5:] == APPENDED_ITEMS, "G11", "the appended text_must_state items are not the eight of the plan")
    judged["verdict"]["text_must_state"] = edited["verdict"]["text_must_state"] = None
    expect(judged == edited, "G11", "the edited expected outcome differs from the judged one beyond text_must_state")


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
    pick = lambda phase, a, b: ctx.members[("c0" if phase == "C0" else "c1") + a][b]
    ctx.cand = {p: strict_json(pick(p, "cand", "candidate.json"), f"{p} candidate") for p in CONFIGS}
    ctx.receipt = {p: strict_json(pick(p, "arch", "archive-receipt.json"), f"{p} archive receipt") for p in CONFIGS}
    ctx.manifest = {p: strict_json(pick(p, "arch", "archive-manifest.json"), f"{p} archive manifest") for p in CONFIGS}
    ctx.phase = {p: strict_json(pick(p, "pods", "phase-status.json"), f"{p} phase status") for p in CONFIGS}
    ctx.summary = strict_json(ctx.members["c5"]["summary.json"], "C5 summary")
    ctx.judged_yaml = yaml.safe_load(ctx.judged.decode("utf-8"))
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


def valid(r):
    return r.get("status") == "complete_valid" and r["consumer"].get("status") == "complete_valid"


def check_g2(ctx):
    for phase in CONFIGS:
        c = ctx.cand[phase]
        expect(c.get("scenario_id") == "C01" and c.get("configuration_id") == phase and c.get("phase") == phase
               and c.get("run_id") == RUN_ID, "G2", f"{phase} candidate identity")
        expect(c.get("source_commit") == HEAD and c.get("synthetic") is True, "G2", f"{phase} candidate source commit or synthetic flag")
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


def recompute(ctx):
    """Each rule of the judged expected outcome, from the candidates, as written there (plan section 4.2).

    Returns the computed values in the order of PINNED."""
    out = []
    results = {}
    for phase, hyp in (("C0", "H1"), ("C1", "H2")):
        lower, upper = WINDOWS[phase]
        reps = ctx.cand[phase]["repeats"]
        micro = [r["consumer"]["reassignment"]["microseconds"] for r in reps]
        counted = [r for r in reps if valid(r) and r["consumer"]["reassignment"]["microseconds"] is not None]
        all_valid = len(reps) == 5 and all(valid(r) for r in reps)
        all_counted = len(reps) == 5 and len(counted) == 5
        holds = all_counted and all(lower <= v < upper for v in micro)
        contradicted = all_counted and any(v >= upper for v in micro)
        result = "holds" if holds else "contradicted" if contradicted else "inconclusive"
        early = [r["repeat"] for r in counted if r["consumer"]["reassignment"]["microseconds"] < lower]
        cnp = [r["consumer"]["counts"]["committed_never_processed"] for r in reps]
        h3 = "holds" if all_valid and all(v >= 1 for v in cnp) else "contradicted" if all_valid and any(v == 0 for v in cnp) else "inconclusive"
        results[phase] = result
        out += [result, early or None, h3, micro]
    verdict = ("confirmed" if results["C0"] == "holds" and results["C1"] == "holds"
               else "contradicted" if "contradicted" in results.values() else "no_verdict")
    out.append(verdict)
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


def check_preconditions(ctx):
    """The five preconditions of the published sentences; returns the number of the first that fails, or 0."""
    all_repeats = [(p, r) for p in CONFIGS for r in ctx.cand[p]["repeats"]]
    if not all(r["consumer"]["kill_count"] == 1 and r["consumer"]["reassignment"]["unavailable_reason"] is None for _p, r in all_repeats):
        return 1
    for _p, r in all_repeats:
        o = r["consumer"]["offset_read"]
        if not (o["outcome"] == "success" and o["requested_partitions"] == list(range(32))
                and all(e["offset"] >= 0 and e["error_code"] == 0 for e in o["offsets"])):
            return 2
    for _p, r in all_repeats:
        c = r["consumer"]["counts"]
        if not (c["processed_again"] == 0 and c["processed_again_extra_rows"] == 0
                and c["processed_once"] + c["committed_never_processed"] == r["counts"]["ok"] == 600):
            return 3
    for p, r in all_repeats:
        s_items, s_node, s_codes = pod_facts(ctx.pods[(p, r["repeat"], "consumer-subject")])
        w_items, w_node, w_codes = pod_facts(ctx.pods[(p, r["repeat"], "consumer-witness")])
        if not (len(s_items) == 1 and len(w_items) == 1 and s_codes and all(c == 137 for c in s_codes)
                and w_codes and all(c == 0 for c in w_codes) and len(s_node) == 1 and s_node == w_node and None not in s_node):
            return 4
    for p in CONFIGS:
        gaps = [o for o in ctx.manifest[p]["objects"] if o.get("gap_code") is not None]
        repeats = sorted(o.get("repeat") for o in gaps)
        if not (len(gaps) == 5 and repeats == REPEATS
                and all(o["gap_code"] == "configured_self_kill" and o["kind"] == "native_monitoring_index"
                        and "-consumer-subject/" in o["source"]["name"] for o in gaps)):
            return 5
    return 0


def check_g3(ctx):
    got = recompute(ctx)
    for (rule, pinned), computed in zip(PINNED, got):
        if computed != pinned:
            if rule.endswith("early_removal"):
                fail("G3", f"{rule} " + ("no finding" if computed is None else f"finding at repeats {computed}")
                     + ", expected " + ("no finding" if pinned is None else f"finding at repeats {pinned}"))
            fail("G3", f"{rule} {computed}, expected {pinned}")
    number = check_preconditions(ctx)
    if number:
        fail("G3", f"precondition {number}")


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
               and ident.get("run_id") == RUN_ID and ident.get("source_commit") == HEAD, "G5", f"{phase} archive receipt identity")
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
        expect(len(modes) == 31, "G6", f"{phase} phase status has {len(modes)} mode entries, not 31")
        for mode in modes:
            allowed = {"consumer-subject": "failed", "consumer-readiness": "ready"}.get(mode.get("mode"), "complete")
            expect(mode.get("outcome") == allowed, "G6", f"{phase} phase status Job {mode.get('job')} outcome {mode.get('outcome')}")


def check_g10(ctx):
    s = ctx.summary
    expect(s.get("measurement_complete") is True and s.get("break_passed") is True, "G10", "mutation summary measurement_complete or break_passed")
    expect(s.get("run_id") == str(ctx.cfg["run"]), "G10", "mutation summary run_id differs from --mutation-run")
    expect(isinstance(s.get("commit"), str) and re.fullmatch(r"[0-9a-f]{40}", s["commit"]) is not None, "G10", "mutation summary commit is not a 40-character SHA")


def check_g8(md, row):
    expect(md.isascii() and row.isascii(), "G8", "the rendered report or README row has a byte outside ASCII")


# ----------------------------------------------------------------------------------------------
# Derived values and rendering


def takeovers(ctx, phase):
    return [r["consumer"]["reassignment"]["microseconds"] / 1e6 for r in ctx.cand[phase]["repeats"]]


def skips(ctx, phase):
    return [r["consumer"]["counts"]["committed_never_processed"] for r in ctx.cand[phase]["repeats"]]


def ratios(ctx, phase):
    return [c / t for c, t in zip(skips(ctx, phase), takeovers(ctx, phase))]


def rng(values, fmt):
    return f"{fmt(min(values))} to {fmt(max(values))}"


def derived(ctx):
    one = lambda v: format(v, ".1f")
    two = lambda v: format(v, ".2f")
    whole = lambda v: str(v)
    allr = [x for p in CONFIGS for x in [float(two(v)) for v in ratios(ctx, p)]]
    return {
        "c0t": rng(takeovers(ctx, "C0"), one), "c1t": rng(takeovers(ctx, "C1"), one),
        "c0s": rng(skips(ctx, "C0"), whole), "c1s": rng(skips(ctx, "C1"), whole),
        "c0x": rng([t - SESSION_TIMEOUT_S["C0"] for t in takeovers(ctx, "C0")], one),
        "c1x": rng([t - SESSION_TIMEOUT_S["C1"] for t in takeovers(ctx, "C1")], one),
        "c0r": rng(ratios(ctx, "C0"), two), "c1r": rng(ratios(ctx, "C1"), two),
        "allr": f"{two(min(allr))} to {two(max(allr))}",
    }


def fill(template, values):
    return template.format(**values)


def plain_paragraphs(values):
    return [fill(p, values) for p in PLAIN_WORDS]


def table(headers, rows):
    lines = ["| " + " | ".join(headers) + " |", "|" + "|".join("---" for _ in headers) + "|"]
    for row in rows:
        lines.append("| " + " | ".join(str(cell) for cell in row) + " |")
    return "\n".join(lines)


def render_row(ctx):
    return fill(README_ROW, derived(ctx))


def results_table(ctx, phase):
    rows = []
    for r, ratio in zip(ctx.cand[phase]["repeats"], ratios(ctx, phase)):
        cnt = r["consumer"]["counts"]
        rows.append([r["repeat"], r["status"], r["consumer"]["status"], format(r["consumer"]["reassignment"]["microseconds"] / 1e6, ".1f"),
                     cnt["committed_never_processed"], cnt["processed_once"], format(ratio, ".2f")])
    return table(["Repeat", "Status", "Consumer status", "Takeover (s)", "Committed, never processed", "Processed once",
                  "Skipped per second of takeover"], rows)


def render_md(ctx, document):
    publication = document["publication"]
    cands = ctx.cand
    values = derived(ctx)
    out = [fill(TITLE, values), "", fill(SUMMARY, values), "", VERDICT_LINE, "", "## In plain words", ""]
    for paragraph in plain_paragraphs(values):
        out += [paragraph, ""]

    out += ["## What was tested", "", WHAT_TESTED, "", "Kill point (from the expected outcome):", "", "> " + publication["kill_point"], "",
            "Window policy (from the expected outcome):", "", "> " + publication["window_policy"], ""]

    out += ["## Results by repeat", "", "**C0, session.timeout.ms 30000**", "", results_table(ctx, "C0"), "",
            "**C1, session.timeout.ms 45000 on the subject**", "", results_table(ctx, "C1"), "", RESULTS_SENTENCE, ""]

    out += ["## Hypotheses", ""]
    out += [fill(h, values) for h in HYPOTHESES]
    out += [""]

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
        f"- Run [{RUN_ID_NUM}]({RUN_URL}), attempt 1, event `workflow_dispatch`, ref `main`, head `{HEAD}`, created 2026-11-23T06:31:38Z, conclusion `success`.",
        "- Both candidates are `complete_valid` with no reasons.",
        "- Both phase-status files report `safety_stop` false, every `consumer-subject` Job `failed` (the subject ends itself with SIGKILL, "
        "as the kill point requires) and every other Job `complete` or, for `consumer-readiness`, `ready`.",
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
        f"- Versions: Confluent.Kafka `{v['confluent_kafka']}`, librdkafka `{v['librdkafka']}`, .NET `{v['dotnet']}`, verifier `{v['verifier']}`, workload generator `{v['workload_generator']}`.",
        f"- Source commit: `{HEAD}`.",
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
        f"(summary SHA-256 `{mutation['summary_sha256']}`). " + MUTATION_SENTENCE, "",
    ]

    out += ["## Not tested", ""] + NOT_TESTED + [""]
    run = ctx.cfg["run"]
    out += ["## Sources", ""] + [s.replace("<run>", str(run)) for s in SOURCES] + [""]
    return "\n".join(out).rstrip("\n") + "\n"


def build_publication(ctx):
    cands = ctx.cand
    cfg = ctx.cfg
    judged = ctx.judged_yaml
    inputs = []
    for key in ZIP_KEYS:
        if key == "c5":
            continue
        phase = "C0" if key.startswith("c0") else "C1"
        for kind, artifact_id, name, k in ARTIFACTS[phase]:
            if k != key:
                continue
            inputs.append({"name": f"{phase} {kind} artifact ZIP", "source": f"artifact {artifact_id}, {name}", "sha256": INPUT_FILES[key][1]})
            for (mk, member), pin in MEMBER_PINS.items():
                if mk == key:
                    inputs.append({"name": f"{phase} {kind} member {member}", "source": f"member {member} of artifact {artifact_id}", "sha256": pin})
    for phase in CONFIGS:
        job_id, job_key, key = LOGS[phase]
        inputs.append({"name": f"{phase} job log", "source": f"job {job_id} ({job_key})", "sha256": INPUT_FILES[key][1]})
    inputs.append({"name": "mutation summary artifact ZIP", "source": f"artifact {cfg['artifact']}, c5-summary-{ctx.summary['run_id']}-{ctx.summary['run_attempt']}",
                   "sha256": cfg["zip_sha"]})
    inputs.append({"name": "mutation summary.json", "source": f"member summary.json of artifact {cfg['artifact']}", "sha256": cfg["summary_sha"]})
    inputs.append({"name": "expected-judged.yaml", "source": f"git show {HEAD}:{EXPECTED_PATH}, the expected outcome the run was judged against", "sha256": JUDGED_SHA})
    inputs.append({"name": EXPECTED_PATH, "source": "working tree after the eight-item edit", "sha256": EDITED_SHA})
    inputs.append({"name": SCENARIO_PATH, "source": f"working tree, equal to {HEAD}", "sha256": SCENARIO_SHA})

    got = dict(zip([r for r, _ in PINNED], recompute(ctx)))
    nodes, subject_codes, witness_codes = {}, {}, {}
    for phase in CONFIGS:
        nodes[phase] = [next(iter(pod_facts(ctx.pods[(phase, r, "consumer-subject")])[1])) for r in REPEATS]
        subject_codes[phase] = [pod_facts(ctx.pods[(phase, r, "consumer-subject")])[2][0] for r in REPEATS]
        witness_codes[phase] = [pod_facts(ctx.pods[(phase, r, "consumer-witness")])[2][0] for r in REPEATS]
    assess = judged["assessment_rules"]
    publication = {
        "item": "C01",
        "workload": "synthetic",
        "run": {
            "workflow_run_id": RUN_ID_NUM, "attempt": 1, "event": "workflow_dispatch", "ref": "main", "head_sha": HEAD,
            "created_at": "2026-11-23T06:31:38Z", "conclusion": "success", "run_id": RUN_ID,
        },
        "expected_outcome": {
            "path": EXPECTED_PATH, "judged_sha256": JUDGED_SHA, "published_sha256": EDITED_SHA,
            "change": "eight verdict.text_must_state items appended by decision 7128051335 point 1; no rule changed",
        },
        "scenario_definition": {"path": SCENARIO_PATH, "sha256": SCENARIO_SHA},
        "publication_decision": {
            "comment_id": 7126257034,
            "url": f"{REPO}/issues/513#issuecomment-7126257034",
        },
        "proposed_verdict": got["verdict"],
        "rule": "scenarios/C01/expected.yaml verdict.contradicted_when: H1 is contradicted or H2 is contradicted.",
        "kill_point": assess["kill_point"],
        "window_policy": assess["window_policy"],
        "hypotheses": {
            "H1_removal_follows_30000": {"configuration": "C0", "result": got["configurations.C0.H1_removal_follows_30000"],
                                         "window_microseconds": list(WINDOWS["C0"])},
            "H2_removal_follows_45000": {"configuration": "C1", "result": got["configurations.C1.H2_removal_follows_45000"],
                                         "window_microseconds": list(WINDOWS["C1"])},
            "H3_k57_latest_reset_skips_messages": {"C0": got["configurations.C0.H3_k57_latest_reset_skips_messages"],
                                                   "C1": got["configurations.C1.H3_k57_latest_reset_skips_messages"], "separate_line": True},
        },
        "early_removal": {
            "C0": {"finding": got["configurations.C0.early_removal"] is not None, "repeats": got["configurations.C0.early_removal"] or []},
            "C1": {"finding": got["configurations.C1.early_removal"] is not None, "repeats": got["configurations.C1.early_removal"] or []},
            "separate_line": True,
        },
        "reassignment_microseconds": {
            "mode": "recorded_only", "separate_line": True,
            "C0": got["configurations.C0.reassignment"], "C1": got["configurations.C1.reassignment"],
        },
        "committed_never_processed": {phase: skips(ctx, phase) for phase in CONFIGS},
        "archives": {
            phase: {
                "archive_complete": ctx.receipt[phase]["archive_complete"], "gaps": ctx.receipt[phase]["gaps"],
                "manifest_sha256": ctx.receipt[phase]["manifest"]["sha256"],
                "configured_self_kill_repeats": sorted(o["repeat"] for o in ctx.manifest[phase]["objects"] if o.get("gap_code") == "configured_self_kill"),
            } for phase in CONFIGS
        },
        "consumer_nodes": nodes,
        "consumer_exit_codes": {phase: {"subject": subject_codes[phase], "witness": witness_codes[phase]} for phase in CONFIGS},
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
            "workflow_run_id": cfg["run"], "commit": ctx.summary["commit"], "measurement_complete": ctx.summary["measurement_complete"],
            "break_passed": ctx.summary["break_passed"], "summary_sha256": cfg["summary_sha"], "includes_run_head": HEAD,
        },
        "inputs": inputs,
    }
    return {"publication": publication, "candidates": {"C0": cands["C0"], "C1": cands["C1"]}}


# ----------------------------------------------------------------------------------------------
# Content checks of the rendered report


def check_g9(ctx, md, row):
    values = derived(ctx)
    title = fill(TITLE, values)
    heading = "\n## In plain words\n"
    expect(md.startswith(title + "\n\n" + fill(SUMMARY, values) + "\n\n" + VERDICT_LINE + "\n\n## In plain words\n\n"), "G9",
           "the report opening differs from the title, summary, verdict line and plain-words heading")
    before = md[:md.index(heading)]
    expect(not any(term in before for term in VERIFIER_TERMS), "G9", "a verifier term appears before the plain words")
    expect(md.index("\n## In plain words\n") < md.index("\n## What was tested\n"), "G9", "the plain words do not come before the first verifier section")
    paragraphs = plain_paragraphs(values)
    position = md.index(heading)
    for number, paragraph in enumerate(paragraphs, 1):
        at = md.find(paragraph + "\n\n", position)
        expect(at >= 0, "G9", f"plain-words paragraph {number} is missing or out of order")
        position = at + len(paragraph)
        if number in (2, 6, 7):
            expect("inference" in paragraph and "inference" in md[at:at + len(paragraph)], "G9", f"plain-words paragraph {number} lacks the word inference")
    publication_kill = ctx.judged_yaml["assessment_rules"]["kill_point"]
    publication_window = ctx.judged_yaml["assessment_rules"]["window_policy"]
    required = [WHAT_TESTED, "> " + publication_kill, "> " + publication_window, RESULTS_SENTENCE, COMMIT_EVIDENCE, BROKER_SENTENCE, SELF_KILL_SENTENCE,
                NODE_SENTENCE, MUTATION_SENTENCE] + [fill(h, values) for h in HYPOTHESES] + NOT_TESTED
    for text in required:
        expect(text in md, "G9", f"required text is missing: {text[:60]}")
    for phrase in ("an inference from the numbers", "Inferred from the librdkafka documentation", "recommends no session timeout"):
        expect(phrase in row, "G9", f"the README row lacks the phrase: {phrase}")
    expect(row.startswith("| C01 |"), "G9", "the README row does not start with the C01 cell")


# ----------------------------------------------------------------------------------------------
# Pipeline


def pipeline(raw, cfg, mode="render", hooks=None, committed=None):
    hooks = hooks or {}

    def hook(stage, *args):
        if stage in hooks:
            hooks[stage](*args)

    cfg = dict(cfg)
    hook("cfg", cfg)
    ctx = new_context(raw, cfg)
    hook("members", ctx)
    hook("working", ctx)
    check_g1(ctx)
    hook("after_g1", ctx)
    check_g11(ctx)
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
    expect(lines[README_LINE - 1].startswith("| C01 |"), "G1", f"README.md line {README_LINE} is not the C01 row")
    (ROOT / "results" / "C01").mkdir(parents=True, exist_ok=True)
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


def case_table(cfg):
    """Each case: (name, expected check code, expected message prefix or None, hooks). One change each."""

    def at(stage, fn):
        return {stage: fn}

    def rep(ctx, phase, number):
        return ctx.cand[phase]["repeats"][number - 1]

    def reassign(phase, number, value):
        def apply(ctx):
            rep(ctx, phase, number)["consumer"]["reassignment"]["microseconds"] = value

        return at("parsed", apply)

    def set_c1(value):
        def apply(ctx):
            for r in ctx.cand["C1"]["repeats"]:
                r["consumer"]["reassignment"]["microseconds"] = value

        return at("parsed", apply)

    def h2_one_at_bound(ctx):
        for r in ctx.cand["C1"]["repeats"]:
            r["consumer"]["reassignment"]["microseconds"] = 59999999
        rep(ctx, "C1", 5)["consumer"]["reassignment"]["microseconds"] = 60000000

    def h1_null(ctx):
        rep(ctx, "C0", 1)["consumer"]["reassignment"].update({"microseconds": None, "unavailable_reason": "different_nodes"})

    def h3_zero(ctx):
        counts = rep(ctx, "C1", 2)["consumer"]["counts"]
        counts["committed_never_processed"] = 0
        counts["processed_once"] = 600

    def processed_again(ctx):
        counts = rep(ctx, "C0", 5)["consumer"]["counts"]
        counts["processed_again"] = 1
        counts["processed_again_extra_rows"] = 1

    def count_sum_short(ctx):
        counts = rep(ctx, "C0", 3)["consumer"]["counts"]
        expect(counts["processed_once"] == 523, "SELFTEST", "C0 repeat 3 processed_once is not 523")
        counts["processed_once"] = 522

    def offset_negative(ctx):
        entries = [e for e in rep(ctx, "C1", 1)["consumer"]["offset_read"]["offsets"] if e["partition"] == 9]
        expect(len(entries) == 1, "SELFTEST", "C1 repeat 1 has no single partition 9 offset")
        entries[0]["offset"] = -1001

    def subject_exit_zero(ctx):
        ctx.pods[("C0", 5, "consumer-subject")]["items"][0]["status"]["containerStatuses"][0]["state"]["terminated"]["exitCode"] = 0

    def node_split(ctx):
        ctx.pods[("C1", 2, "consumer-witness")]["items"][0]["spec"]["nodeName"] = "another-node"

    def self_kill_missing(ctx):
        entries = [o for o in ctx.manifest["C1"]["objects"] if o.get("gap_code") == "configured_self_kill" and o.get("repeat") == 3]
        expect(len(entries) == 1, "SELFTEST", "C1 manifest has no single repeat-3 configured_self_kill entry")
        entries[0]["gap_code"] = None

    def md_replace(old, new):
        def apply(box):
            expect(old in box[0], "SELFTEST", f"case text not found: {old[:40]}")
            box[0] = box[0].replace(old, new, 1)

        return {"md": apply}

    def edit_expected(old, new):
        def apply(ctx):
            text = ctx.working_expected.decode("utf-8")
            expect(old in text, "SELFTEST", f"case text not found: {old[:40]}")
            ctx.working_expected = text.replace(old, new, 1).encode("utf-8")

        return at("after_g1", apply)

    def drop_last_item(ctx):
        lines = ctx.working_expected.decode("utf-8").split("\n")
        start = max(i for i, line in enumerate(lines) if line == "    - >-")
        end = next(i for i in range(start, len(lines)) if lines[i] == "")
        ctx.working_expected = "\n".join(lines[:start] + lines[end:]).encode("utf-8")

    def committed_edit(files):
        expect("| 1 | complete_valid |" in files["md"], "SELFTEST", "committed report has no repeat 1 row")
        files["md"] = files["md"].replace("| 1 | complete_valid |", "| 1 | complete_invalid |", 1)

    def mutation_hash(c):
        c["summary_sha"] = c["summary_sha"][:-1] + ("0" if c["summary_sha"][-1] != "0" else "1")

    def summary_set(key, value):
        return at("parsed", lambda ctx: ctx.summary.__setitem__(key, value))

    # The rendered paragraphs are needed for the report cases; they are recomputed from the real inputs below.
    paragraphs = cfg["paragraphs"]

    return [
        ("hash_c0_candidate", "G1", None, at("members", lambda ctx: ctx.members["c0cand"].__setitem__("candidate.json", flip(ctx.members["c0cand"]["candidate.json"])))),
        ("expected_yaml_edit", "G1", None, at("working", lambda ctx: setattr(ctx, "working_expected", flip(ctx.working_expected)))),
        ("mutation_hash", "G1", None, at("cfg", mutation_hash)),
        ("c1_repeat_status", "G2", None, at("parsed", lambda ctx: rep(ctx, "C1", 3)["consumer"].__setitem__("status", "incomplete"))),
        ("versions_differ", "G2", None, at("parsed", lambda ctx: ctx.cand["C1"]["versions"].__setitem__("librdkafka", "2.15.0"))),
        ("h1_at_upper_bound", "G3", "configurations.C0.H1_removal_follows_30000 contradicted, expected holds", reassign("C0", 2, 45000000)),
        ("h1_below_upper_bound", "G3", "configurations.C0.reassignment", reassign("C0", 2, 44999999)),
        ("h1_at_lower_bound", "G3", "configurations.C0.reassignment", reassign("C0", 4, 27000000)),
        ("h1_early", "G3", "configurations.C0.H1_removal_follows_30000 inconclusive, expected holds", reassign("C0", 4, 26999999)),
        ("h1_null", "G3", "configurations.C0.H1_removal_follows_30000 inconclusive, expected holds", at("parsed", h1_null)),
        ("h2_in_window", "G3", "configurations.C1.H2_removal_follows_45000 holds, expected contradicted", set_c1(59999999)),
        ("h2_one_at_bound", "G3", "configurations.C1.reassignment", at("parsed", h2_one_at_bound)),
        ("h2_at_lower_bound", "G3", "configurations.C1.reassignment", reassign("C1", 3, 42000000)),
        ("h2_early", "G3", "configurations.C1.early_removal", reassign("C1", 3, 41999999)),
        ("h3_zero", "G3", "configurations.C1.H3_k57_latest_reset_skips_messages contradicted, expected holds", at("parsed", h3_zero)),
        ("processed_again_one", "G3", "precondition 3", at("parsed", processed_again)),
        ("count_sum_short", "G3", "precondition 3", at("parsed", count_sum_short)),
        ("kill_count", "G3", "precondition 1", at("parsed", lambda ctx: rep(ctx, "C1", 4)["consumer"].__setitem__("kill_count", 2))),
        ("offset_read_failed", "G3", "precondition 2", at("parsed", lambda ctx: rep(ctx, "C0", 1)["consumer"]["offset_read"].__setitem__("outcome", "kafka_exception"))),
        ("offset_negative", "G3", "precondition 2", at("parsed", offset_negative)),
        ("subject_exit_zero", "G3", "precondition 4", at("parsed", subject_exit_zero)),
        ("node_split", "G3", "precondition 4", at("parsed", node_split)),
        ("self_kill_missing", "G3", "precondition 5", at("parsed", self_kill_missing)),
        ("root_consumer_sum", "G4", None, at("parsed", lambda ctx: ctx.cand["C0"]["consumer"]["counts"].__setitem__("committed_never_processed", 365))),
        ("root_producer_sum", "G4", None, at("parsed", lambda ctx: ctx.cand["C1"]["counts"].__setitem__("ok", 2999))),
        ("archive_gap", "G5", None, at("parsed", lambda ctx: ctx.receipt["C0"].__setitem__("gaps", ["x"]))),
        ("archive_phase", "G5", None, at("parsed", lambda ctx: ctx.receipt["C1"]["identity"].__setitem__("phase", "C0"))),
        ("phase_safety_stop", "G6", None, at("parsed", lambda ctx: ctx.phase["C0"].__setitem__("safety_stop", True))),
        ("subject_mode_complete", "G6", None, at("parsed", lambda ctx: next(m for m in ctx.phase["C1"]["modes"] if m["repeat"] == 1 and m["mode"] == "consumer-subject").__setitem__("outcome", "complete"))),
        ("mutation_incomplete", "G10", None, summary_set("measurement_complete", False)),
        ("mutation_run_id", "G10", None, summary_set("run_id", "1")),
        ("non_ascii", "G8", None, md_replace("## Sources", "## Sources " + chr(0x2013))),
        ("missing_plain_words", "G9", None, md_replace(paragraphs[3] + "\n\n", "")),
        ("verifier_term_early", "G9", None, md_replace("In run 45124695288 on", "In run 45124695288 reassignment on")),
        ("inference_label_dropped", "G9", None, md_replace(paragraphs[5], paragraphs[5].replace("This is an inference from", "This is from"))),
        ("expected_rule_changed", "G11", None, edit_expected("confirmed_when: H1 holds and H2 holds.", "confirmed_when: H1 holds.")),
        ("expected_item_missing", "G11", None, at("after_g1", drop_last_item)),
        ("report_edit", "G7", None, {"committed": committed_edit}),
    ]


def self_test(raw, cfg):
    problems = 0
    try:
        base = pipeline(copy.deepcopy(raw), cfg)
        print("PASS real_inputs")
    except Fail as error:
        print(f"FAIL real_inputs: {error}")
        return 1
    cfg = dict(cfg)
    ctx = new_context(raw, cfg)
    parse(ctx)
    cfg["paragraphs"] = plain_paragraphs(derived(ctx))
    cases = case_table(cfg)
    print(f"{len(cases)} cases")
    for name, code, prefix, hooks in cases:
        mode = "check" if code == "G7" else "render"
        try:
            pipeline(copy.deepcopy(raw), cfg, mode=mode, hooks=hooks)
        except Fail as error:
            if error.code == code and (prefix is None or error.text.startswith(prefix)):
                print(f"PASS {name}")
            else:
                print(f"FAIL {name}: failed at {error.code}, expected {code} {prefix or ''}: {error}")
                problems += 1
        else:
            print(f"FAIL {name}: passed unexpectedly, expected {code}")
            problems += 1
    return 1 if problems else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--inputs", required=True, help="directory holding the downloaded artifacts and job logs")
    parser.add_argument("--mutation-run", required=True, type=int)
    parser.add_argument("--mutation-artifact-id", required=True, type=int)
    parser.add_argument("--mutation-zip-sha256", required=True)
    parser.add_argument("--mutation-summary-sha256", required=True)
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--check", action="store_true")
    group.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    cfg = {"run": args.mutation_run, "artifact": args.mutation_artifact_id,
           "zip_sha": args.mutation_zip_sha256, "summary_sha": args.mutation_summary_sha256}
    try:
        raw = read_inputs(args.inputs)
        if args.self_test:
            return self_test(raw, cfg)
        if args.check:
            pipeline(raw, cfg, mode="check")
            print("PASS check")
            return 0
        result = pipeline(raw, cfg)
        write_outputs(result)
        print(result["row"])
        return 0
    except Fail as error:
        print(error)
        return 1


if __name__ == "__main__":
    sys.exit(main())

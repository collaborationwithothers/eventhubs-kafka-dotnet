# B10 warm leader contract-grade plan

Revision 1, issue #404. Short name: `B10 warm leader`. Baseline: `main` at `e74849ea716b6c4b8fa6c5143c6c4175c7038eea`, read 2026-11-11.

This pull request makes the positive restart outcome reachable on Event Hubs. Each child arm first sends one warm-up record to its own partition, waits for that record's successful delivery and for a statistics report showing the partition leader `UP`, and only then sends its originals. The restart probe producers stop inheriting C1's 100 ms `message.timeout.ms` and use the baseline 300000; the child also sets `linger.ms` 0. The pull request carries the ADR-021 amendment, the LoadGen and phase-reader code, and the offline proof in the merged offline image test. `docs/blueprint.md` is protected, so Hari merges.

## 0. Revision history

- Revision 1: first revision.

## 1. Goal and authority

Goal: an original can be transmitted, unconfirmed at the kill, then resent by the successor and seen as a cross-restart duplicate, without changing the kill position, the verdict rules or the C1 timeout Job.

Why (evidence, not authority): the trial of run 43010596896 ([6796480692](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6796480692)) and its diagnosis ([6796659976](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6796659976)): every abrupt producer copies the resolved C1 settings (`src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs` lines 191 to 193), so it ran with `message.timeout.ms` 100; the successor's resends expired unsent before their cold leader connection came up; the cross-restart originals were almost certainly never transmitted before the kill.

Authority. The item state file `items/404/state.md` on `main` lists the decisions in force up to comment 6790675342. New since then, in creation order:

- [6794365032](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6794365032): retroactive acceptance of the `LocalKafkaFixture.cs` change in pull request 432; "it does not widen the plan further".
- [6797472633](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797472633), option A: continue; "each arm's partition leader connection is open before its originals, and the restart probe producers (child and successor) get a delivery timeout that covers connection set-up, while the C1 timeout Job keeps its own settings"; this changes ADR-021; "a further trial needs Hari's decision".
- [6797974611](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797974611), "A B A": Q1 A, one warm-up record per child arm with the arm's own key, wait for its successful delivery report and then a statistics report showing that partition's leader broker `UP`; warm-up records are "their own journal record kind and cohort; they never enter arm counts or pairs"; this "replaces the binding in ADR-021 protocol rule 5 and narrows rules 1 and 8". Q2 B, the kill stays immediately after the cut; the child's probe producer sets `linger.ms` 0; the successor may keep the baseline. Q3 A, probe producers (child and successor) use the baseline `message.timeout.ms` 300000; idempotence and `max.in.flight` 5 stay as in C1; the C1 timeout Job is unchanged; no successor warm-up. Four live-only assumptions are named "to be listed in the design" (section 7). "This is not design approval."
- [6797990874](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797990874): one pull request carries the ADR-021 amendment, the code and the offline proof; Hari merges.

Decisions in force that this plan keeps: schema 3, the 120 s release, the existing control drain bound, the sticky torn-record gap and no-kill exit 3 ([6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785)); `CutOrdinal` 4 ([6769269811](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769269811)); `ProbeRate` 100 ([6769312013](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769312013)); the no-kill end ([6768228960](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768228960)); the offline test's mechanism and accepted residuals ([6787880185](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787880185), [6788215099](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788215099), [6788464379](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788464379)); execution part 2 stays free of verifier changes ([6787849492](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787849492)).

Design text changed: docs/blueprint.md ADR-021 protocol rules 1, 3, 5, 6, 7, 8 and 15 and its "Classification" bullet (section 3.1). ADR-021 protocol rule 15 forbids changing the pre-acceptance statistics gate after the trial; decision 6797974611 is Hari's design decision that changes it, and the amendment records that.

Implementing tool: not named for this pull request. Hari names it under docs/agents/factory.md "Item lifecycle" step 2.

## 2. Scope

### 2.1 Files allowed to change

1. `docs/blueprint.md`: ADR-021 (section 3.1) and the amendment-index paragraph near the top of section 4 that lists B10 amendments (line 12 at baseline).
2. `docs/operations/b10-abrupt-contract.md`: the settings, journal kinds, probe sequence and failure names of section 3.
3. `src/Parchwell.LoadGen/Run/Abrupt/*.cs`.
4. `infra/scripts/phase/b10_abrupt.py`.
5. `infra/scripts/phase/tests/test_b10_abrupt.py`.
6. `tests/Parchwell.LoadGen.Tests/Abrupt/*.cs`, existing and new files. Existing tests may change their fixture journals and fake producers to supply the records this plan requires; no existing assertion is loosened, skipped or deleted.
7. `tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs`: one optional advertised-address input (section 3.6). Existing callers keep their behaviour.
8. `tests/Parchwell.ArchitectureTests/B10AbruptFailureSiteTests.cs`, only if the two new failure names require it.
9. `items/404/state.md` and new `items/404/b10-warm-leader-receipt.md` (the implementing session writes both; docs/agents/factory.md "Item state file").

### 2.2 Must not change

- `scenarios/**`: C1 keeps `message.timeout.ms` 100 and `max.in.flight` 5 for the C1 timeout Job.
- The ordinary LoadGen host and every file under `src/` outside `Run/Abrupt/`, including `src/Parchwell.Kafka/**` (`BaselineSettingsCatalog.cs` keeps 300000, 5 and 15000).
- `src/Parchwell.Verifier*/**`, `tests/Parchwell.Verifier*/**` (decision 6787849492; verifier classification belongs to its own pull request).
- `.github/**`, including `b10-offline-image.yml` and its `--filter-method` test name; `charts/**`; `infra/**` other than items 4 and 5; `scripts/**`; `results/**`; any `expected.yaml`; `*.csproj`, `Directory.*.props`, `*.sln*`, lock files; `AGENTS.md`, `CLAUDE.md`; every other file under `docs/`.
- No new project, assembly reference, NuGet package or tool.
- The offline test method name `Given_BuiltImage_When_ParentChildAndSuccessorRunInContainers_Then_JournalExitAndResendsAreObserved`.

### 2.3 Required CI and protected paths

- `.NET verification` (required): `scripts/verify-dotnet.sh` builds with warnings as errors and runs `tests/Parchwell.LoadGen.Tests` and `tests/Parchwell.ArchitectureTests`. The offline image test returns early there because `PARCHWELL_B10_OFFLINE_IMAGE` is unset.
- `Lifecycle verification`: "Verify lifecycle with stub commands" runs `infra/scripts/phase/tests` through `infra/scripts/tests/test_phase_bridge.py`; "Verify the load generator accepts the rendered chart environment" runs `charts/parchwell/tests/check-loadgen-host.sh`, which runs `infra/scripts/phase/tests/check_b10_abrupt_producers.py`.
- `docs-advisory`: advisory documentation checks.
- Not reached: `verifier-c5-mutation.yml` (no verifier change). `b10-offline-image.yml` is `workflow_dispatch` only; Hari runs it on `main` after merge.
- Protected paths touched: `docs/blueprint.md` (`/docs/` in `.github/CODEOWNERS`). `docs/operations/` is listed there with no owner. Hari merges.

## 3. External contracts

### 3.1 ADR-021 amendment text

The implementer writes these statements into the named rules of ADR-021, keeping every other sentence. Wording may be adjusted for the surrounding sentence; the stated facts, values, sources and failure names may not.

- Protocol rule 1, after "Both arms use equal configured payload shape, settings, rate and cut ordinal.": "Before each arm's first original, the child produces one warm-up record with that arm's key and payload bytes, its own attempt ID and sequence -1, and waits for its successful delivery report. librdkafka connects only to brokers it needs, including leaders for partitions being produced to ([Sparse connections, INTRODUCTION.md v2.16.0](https://github.com/confluentinc/librdkafka/blob/v2.16.0/INTRODUCTION.md#sparse-connections)); that a producer opens a leader connection only once a message is queued for that leader is inferred from [rdkafka_broker.c v2.16.0](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c) (debug text "message(s) queued but broker not up"), not documented. The `consistent_random` partitioner sends equal keys to one partition ([CONFIGURATION.md v2.16.0](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md), `partitioner`: CRC32 hash of key). Warm-up records are their own journal kinds and their own `warmup` cohort: authorized attempts of the first restart instance that never enter timeout totals, arm counts, unconfirmed sets, resends or pairs. The first restart instance's producer and the successor's producer use C1's settings except `message.timeout.ms` 300000, the baseline value, so their local delivery timeout covers connection set-up (`socket.connection.setup.timeout.ms` defaults to 30000, CONFIGURATION.md v2.16.0); the child's producer also sets `linger.ms` 0, and the successor keeps the baseline `linger.ms` 5. The C1 timeout Job keeps C1's settings. These values are policy ([decision 6797974611](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797974611)), not measurements."
- Protocol rule 3, in the list of records the producing thread appends: "`warmup_prepared` before the warm-up invocation"; and after the callback sentence: "The warm-up delivery callback enqueues a `warmup_terminal` record through the same queue." Add: "A warm-up record is not a probe attempt: it is never `unreported_seen` or `unreported_unseen`."
- Protocol rule 5, replace its first sentence with: "For each arm, require the first parsed statistics report captured after that arm's durable `warmup_terminal` record, and before its first original's acceptance, in which the warm-up's delivered partition has a leader broker in state `UP`: `topics.<topic>.partitions.<partition>.leader` equals the `nodeid` of exactly one entry of `brokers`, and that entry's `state` is `UP` ([STATISTICS.md v2.16.0](https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md)). The child journals a binding of that report (name, length, SHA-256, sequence, time, warm-up attempt ID and partition) before that acceptance. A sampled `UP` shows the connection was up when librdkafka built the report, not at any later instant." Keep "No post-acceptance report gate; `statistics.interval.ms` stays unchanged."
- Protocol rule 6, after the kill unconfirmed set sentence: "Warm-up records belong to no unconfirmed set."
- Protocol rule 7, after "No-kill path:": "A warm-up that is not delivered within the existing `ProducerDrainTimeout` is `b10_abrupt_warmup_failed`, and a bound report whose partition leader is not `UP` is `b10_abrupt_leader_not_up`; each ends the child on the no-kill path. A cross-restart warm-up does not admit the arm's first original." Keep "the kill ... immediately after the durable `kill_intent`" unchanged.
- Protocol rule 8, after the resend sentence: "The successor sends no warm-up; under rule 1's settings its resends wait for its leader connection instead of expiring at C1's 100 ms."
- "Classification" bullet, after its first sentence: "Consumed warm-up records join their `warmup_prepared` journal records and form the separate `warmup` cohort; they enter no probe or timeout count."
- Protocol rule 15, a new final paragraph: "Amendment of 2026-11-11 ([decision 6797472633](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797472633), [decision 6797974611](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797974611), [decision 6797990874](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797990874)): the diagnostic trial of run 43010596896 found that the probe producers inherited C1's `message.timeout.ms` 100, so the successor's resends expired before their leader connection came up ([diagnosis 6796659976](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6796659976)). Rules 1, 3 and 5 to 8 add the per-arm warm-up, the leader-readiness binding and the probe settings. This is Hari's design change to the pre-acceptance statistics gate, outside the mechanical changes this rule otherwise allows; the eligibility threshold, control gate, verdict rules, restart subject and expectations are unchanged. The kill stays immediately after the cut. The child cannot observe whether an original is on the wire at the kill: librdkafka documents no per-message transmission signal, and statistics granularity is 1000 ms (`statistics.interval.ms`, CONFIGURATION.md v2.16.0); transmission is observed only when the verifier consumes the original. Only a live run can show that Event Hubs connection set-up stays inside the timeout, that the leader does not change between warm-up and originals, that the produce round trip is long enough for an original to be in flight at the kill, and that Event Hubs appends a fully sent request after the client process dies. A further trial needs Hari's recorded decision."
- Amendment-index paragraph (line 12 at baseline), one added sentence: "The B10 warm-leader change amends ADR-021 protocol rules 1, 3, 5 to 8 and 15 and its classification bullet under [decision 6797472633](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797472633), [decision 6797974611](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797974611) and [decision 6797990874](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797990874); Hari's merge of its pull request records the amendment."

### 3.2 Probe producer settings

| Producer | Effective settings |
| --- | --- |
| C1 timeout Job | Unchanged: resolved C1 `LoadGenProducer` (`message.timeout.ms` "100", `max.in.flight` "5", `enable.idempotence` "true", baseline otherwise) |
| Restart child | Resolved C1 `LoadGenProducer`, then `message.timeout.ms` = "300000" and `linger.ms` = "0" |
| Successor | Resolved C1 `LoadGenProducer`, then `message.timeout.ms` = "300000"; `linger.ms` stays baseline "5" |

The expected-settings dictionary that `B10AbruptStatistics` compares with the actual configuration, and the settings that `capabilities.json` records, are these effective values for each abrupt role. The values are constants in the abrupt code, not new `B10Abrupt__` keys, and not values in `scenarios/B10/scenario.yaml`.

### 3.3 Warm-up record on the wire

Topic `document-events` (the existing `options.Topic`). Key: the arm key `B10/{run_id}/C1/1/{arm}`. Value: the arm's probe payload bytes. Headers, in this order: `instance-id` = instance ID, `attempt-id` = `{instance_id}-{arm}-warmup`, `sequence` = `-1`. No partition is set by the caller; the configured partitioner places it.

### 3.4 Journal (schema 3, kinds added)

`schema_version` stays 3. Closed member lists:

| Kind | Writer | `data` members |
| --- | --- | --- |
| `warmup_prepared` | Producing thread, before the warm-up `Produce` | `attempt_id`, `arm`, `key`, `payload_length`, `payload_sha256` |
| `warmup_terminal` | Journal writer thread, from the warm-up delivery callback through the existing bounded queue | `attempt_id`, `completion_source` (`callback`), `delivered`, `error_code`, `persistence_status`, `partition`, `offset` |
| `pre_acceptance_report` | Unchanged position | existing members plus `warmup_attempt_id`, `partition` |

`post_drain_report` keeps its existing members. `warmup_prepared` is a producing kind; `warmup_terminal` is not. `partition` and `offset` are integers; when `delivered` is true both are at least 0.

Order rules, per arm, enforced by every reader: exactly one `warmup_prepared`; it precedes that arm's ordinal-0 `prepared`; the cross-restart `warmup_prepared` comes after the control arm is complete (the existing control-completeness condition for cross-restart `prepared`); exactly one `warmup_terminal` with that attempt ID, after its `warmup_prepared` and before the arm's ordinal-0 `prepared`; that terminal has `delivered` true and `error_code` 0, with `persistence_status` in the set the existing reader already permits for error code 0; the arm's `pre_acceptance_report` has `warmup_attempt_id` equal to that attempt ID, `partition` equal to the terminal's partition, and `report_capture_utc` strictly later than the terminal's `observed_utc`.

### 3.5 Leader-readiness predicate `LeaderUp(report, topic, partition)`

On the raw statistics JSON: true only when `topics[topic].partitions[decimal string of partition].leader` is an integer L at least 0, exactly one value in `brokers` has `nodeid` equal to L, and that value's `state` is the string `UP`. Anything else is false: missing topic, partition or field, L of -1, no or two matching brokers, or any other state (for example `INIT`, `CONNECT`, `DOWN`). The child applies it to the report it binds; the phase reader applies it again to the published report bytes.

### 3.6 Failure names

Added to the no-kill reasons (C# `B10AbruptWire.NoKillReasons`, the phase reader's accepted names, the contract page): `b10_abrupt_warmup_failed` (warm-up synchronously rejected, delivered false, any non-zero error code, or no `warmup_terminal` within the existing `ProducerDrainTimeout`) and `b10_abrupt_leader_not_up` (the bound report fails `LeaderUp`). A missing report within the existing statistics wait budget (`StatisticsCompletionTimeout`, or `CleanupTimeout` when unset) stays `b10_abrupt_sample_missing`. Reader outcomes: a warm-up record that breaks section 3.4's structure or order is the existing capture gap `malformed_frame`; a `pre_acceptance_report` whose warm-up fields, capture time or `LeaderUp` fail is the existing `b10_abrupt_report_binding_invalid`. The phase reader rejects such a first publication through its existing report-binding failure path, so the successor is not released.

### 3.7 Offline image test

Same test method and class. Additions:

- A TCP proxy in the test process listens on `127.0.0.1` at an ephemeral port. `LocalKafkaFixture` gains an optional input that makes the broker's `PLAINTEXT` advertised listener `127.0.0.1:<proxy port>`; without it the fixture behaves as today. Every Kafka client in the test, including the test's admin client, the parent's child, the successor and the test consumer, bootstraps at the proxy.
- Per accepted connection, the proxy waits `ConnectDelay` = 500 ms before it opens its upstream connection to the broker. Client-to-broker bytes are forwarded with no added delay. Each broker-to-client chunk is delivered no earlier than `ResponseDelay` = 500 ms after the proxy read it, in order. When the client side closes, bytes already read from the client are still forwarded upstream before the upstream side closes. Both values are test policy, not Event Hubs measurements.
- The test consumer's read window becomes 60 s (from 20 s).
- The 900 s test bound, the container settings and every existing assertion stay.
- The observation line keeps its keys and order and appends, after `consumed_resends`: ` warmup_records=<count of warmup_terminal records> kill_originals_consumed=<count> connect_delay_ms=500 response_delay_ms=500`.

## 4. Requirements

- R1. WHEN the restart child or the successor constructs its producer THEN its configuration has the section 3.2 values, and the statistics guard and `capabilities.json` use them. WHEN the C1 timeout Job runs THEN nothing about it changes.
- R2. WHEN the control arm starts (after the existing initial report wait) or the cross-restart arm starts (after the control arm is complete) THEN, before the arm's ordinal-0 `prepared`, the child appends a durable `warmup_prepared` and calls `Produce` once with the section 3.3 record.
- R3. WHEN the warm-up delivery callback runs THEN it only enqueues `warmup_terminal` (no file or network I/O). The producing thread waits until that record is durable, bounded by the existing `ProducerDrainTimeout`. It continues only when `delivered` is true and `error_code` is 0. Otherwise, or when `Produce` throws or the bound runs out, the child takes the no-kill path with `b10_abrupt_warmup_failed`.
- R4. WHEN the warm-up has succeeded THEN the child waits, within the existing statistics wait budget, for the first parsed report captured after the `warmup_terminal` record's `observed_utc`. No report: `b10_abrupt_sample_missing`. `LeaderUp` false on that report: `b10_abrupt_leader_not_up`. True: that report is the arm's pre-acceptance report, bound after ordinal 0's `prepared` and before its `accepted`, with the section 3.4 members.
- R5. WHEN either arm's warm-up or leader check fails THEN the existing no-kill rules apply with `cross_restart_admitted` false: the producer is flushed within `ProducerDrainTimeout`, disposed, and the child exits 3. The warm-up never sets cross-restart admission.
- R6. WHEN the originals run THEN pacing, ordinals, cut, `kill_intent`, the self-kill immediately after it, and the ban on `Flush` and `Dispose` after the first cross-restart original's admission are unchanged.
- R7. WHEN the successor runs THEN it sends no warm-up and its resends are unchanged except for R1's settings.
- R8. WHEN `B10AbruptJournal.Read` reads a journal THEN it enforces sections 3.4 and 3.6, and the control and kill unconfirmed sets are computed from `prepared` originals only, as today.
- R9. WHEN the phase reader accepts a first publication THEN it accepts the new kinds and members, enforces sections 3.4 to 3.6 including `LeaderUp` on the published `<arm>-pre-acceptance-report.json` bytes, and otherwise rejects as section 3.6 says.
- R10. WHEN the offline image test runs THEN, besides its existing assertions, it asserts: for each arm, the section 3.4 order with `delivered` true; each bound pre-acceptance report satisfies `LeaderUp`; no `terminal` record in the child journal and no successor ledger row has `error_code` -192; at least one kill-set original was consumed with instance ID `first`, and its linked successor resend was consumed at a different offset with equal key, `sequence` header and payload hash; the proxy accepted at least one connection while each role ran.
- R11. WHEN the pull request merges THEN ADR-021 and the contract page carry sections 3.1 to 3.6, stated once in the file that owns each rule, with other files citing it.

## 5. Invariants and safety rules

- No Azure credential and no Azure call; no workflow dispatched by an agent; no trial. A further trial needs Hari's decision (6797472633).
- The C1 timeout Job, C0, the scenario file, the verdict rules, eligibility, the restart subject and the expected outcomes do not change.
- Callbacks still do no file or network I/O; the journal stays create-new, flushed per record, within its existing limits.
- No number from the offline test is written as an Event Hubs, AKS or librdkafka fact (AGENTS.md hard safety rule 11). The 500 ms delays are test policy.
- Warm-up records never enter any unconfirmed set, resend, pair or count.

## 6. Test map

Common inputs: run ID `b10d-1-1`, instance `first`, topic `document-events`. Statistics fixtures are minimal raw JSON objects with `topics` and `brokers` as section 3.5 reads them, inside reports that also pass the existing guards.

| Requirement | Test (LoadGen unless marked Python) | Inputs and literal expectations |
| --- | --- | --- |
| R1 | Settings test on the abrupt host's producer configuration for roles `child` and `successor` | child: `message.timeout.ms` "300000", `linger.ms` "0", `enable.idempotence` "true", `max.in.flight` "5"; successor: "300000", `linger.ms` "5". Existing `B10AbruptHostRegistrationTests` still asserts the scenario role value "100". |
| R2, R4 | Producer test, fake producer delivering every record | Journal kind order per arm: `warmup_prepared`, `warmup_terminal`, `prepared` (ordinal 0), `pre_acceptance_report` with `warmup_attempt_id` `first-same_instance-warmup` and the fake's partition, `accepted`. Warm-up message headers `instance-id` "first", `attempt-id` `first-same_instance-warmup`, `sequence` "-1", key equal to the arm key. |
| R3 | Producer tests, four cases | `Produce` throws on the warm-up; warm-up delivered with error -192; warm-up never delivered (bound 1 s in the test options); each gives `no_kill` reason `b10_abrupt_warmup_failed`, `cross_restart_admitted` false, no ordinal-0 `prepared` for that arm. Cross-restart warm-up failure after a complete control arm gives the same reason and exit 3. |
| R4 | Producer tests | Bound report with leader state `INIT`: `b10_abrupt_leader_not_up`. No report within the budget: `b10_abrupt_sample_missing`. A report captured before `warmup_terminal`'s time is not selected. |
| R3 | Callback test | The warm-up callback performs no journal write on the callback thread; the record arrives through the queue (existing callback-isolation pattern). |
| R5, R6 | Existing producer and end-to-end tests adapted | Unchanged assertions on cut, `kill_intent`, self-kill and no-kill exits pass with warm-ups present. |
| R7 | Successor test | No `warmup_prepared` or warm-up message from the successor; its invocations equal the kill set. |
| 3.5 | `LeaderUp` table test, C# and Python with the same cases | true: partition "0" leader 1, one broker nodeid 1 `UP`. false: state `INIT`; `CONNECT`; `DOWN`; leader -1; partition missing; topic missing; leader 1 with two brokers nodeid 1 both `UP`; leader 1 with only nodeid 2 `UP`; `leader` not an integer. |
| R8 | Journal reader tests on valid and mutated journals | Valid journal with both warm-ups: no gaps, no failures. Missing cross-restart `warmup_prepared`: `malformed_frame`. `warmup_terminal` after ordinal-0 `prepared`: `malformed_frame`. Two control `warmup_prepared`: `malformed_frame`. Terminal `delivered` false in a journal with `kill_intent`: `malformed_frame`. Report `warmup_attempt_id` or `partition` mismatch: `b10_abrupt_report_binding_invalid`. Report capture time equal to the terminal's time: `b10_abrupt_report_binding_invalid`; 100 ns later (one tick of the `O` format): accepted. Unconfirmed sets of the valid journal equal those computed without the warm-up records. |
| R9 | Python `test_b10_abrupt.py`, same journal cases plus report bytes | Same outcomes as R8 through the phase reader's rejection path; a valid publication whose report bytes fail `LeaderUp` is rejected; new failure names are accepted names. |
| R10 | Offline image test (by hand and by Hari's workflow run) | Section 3.7 literals; existing assertions unchanged. |
| R11 | `npm run check:docs`; cold review | Rule texts present once; links resolve. |

Image mutations, each applied once in a scratch worktree, image rebuilt, test run once, expected to fail (fixed count 2, no cap needed):

| Mutation | Expected failing assertion |
| --- | --- |
| M1. The child's probe `message.timeout.ms` set back to "100" | R10: a -192 terminal or ledger row, or a resend not consumed |
| M2. The child skips the warm-up and binds the earlier report as before | R10: warm-up order missing, or the journal reader's `malformed_frame` |

The predicate map of docs/agents/pull-requests.md: the phase reader and journal reader predicates above are mapped in this table; no verifier predicate changes.

## 7. Deployment assumptions

Live-only, named in decision 6797974611; no offline test can prove them. Each is listed under section 9 until Hari accepts it by name.

- LA1. Event Hubs leader connection set-up stays inside the probe delivery timeout (300000 ms) and the existing `ProducerDrainTimeout`.
- LA2. The partition leader, and its connection, do not change between the warm-up's delivery and the arm's originals. The connection part extends the decision's wording; the offline proxy never drops a connection.
- LA3. The Event Hubs produce round trip is long enough for at least one original to be on the wire, unacknowledged, at the kill, and for the control arm's set at its cut to be nonempty. Inference from policy: with `linger.ms` 0 the fifth original leaves just before the kill; the trial's single reading put its acceptance about 1.9 ms before `kill_intent`. If the round trip is shorter, the outcome is `kill_too_late` or an unseen-only result, not a positive.
- LA4. Event Hubs appends a fully sent produce request after the client process dies.
- LA5 (added by this plan). On Event Hubs, librdkafka statistics give the warm-up's partition a `leader` that matches the `nodeid` of one broker entry. One trial reading supports it: diagnosis 6796659976 names the resends' leader as broker node 7.

Offline proof, by section 3.7 against a local Kafka broker, not Event Hubs: the warm-up waits out a delayed connection and the bound report shows the leader `UP` (R10); the successor's resends survive delayed connections and delayed responses (R10, M1); an original sent before the kill is appended and consumed alongside its resend (R10). The offline test's accepted residuals DA-i to DA-iii and the resource-request omission (decisions 6787880185, 6788215099, 6788464379) still apply.

## 8. Acceptance commands

Run by the implementer before the cold review, in its Linux workspace:

1. `./scripts/verify-dotnet.sh`: exit 0, warnings as errors, no skipped test.
2. `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'`: exit 0.
3. `bash charts/parchwell/tests/check-loadgen-host.sh`: exit 0.
4. `npm run check:docs`: each warning in a changed file fixed or explained under docs/agents/pull-requests.md rule 8.
5. The offline image test by the build and run steps of `.github/workflows/b10-offline-image.yml`, run by hand with Docker: 2 runs (fixed count), each passing and printing one section 3.7 line, recorded in the receipt. Then M1 and M2 once each. A failure is a finding for the receipt; the implementer never changes an assertion to pass.

Caps per implementing session (decision 6788754429's pattern, and the control session's instruction): 20 distinct verification-failure root causes and 20 distinct review-finding root causes. Exhaustion of either: `push -wip and hand off to the next session`. A continuation session starts its own count.

After Hari merges: Hari runs `b10-offline-image.yml` on `main`. No agent dispatches it or the trial.

## 9. Open questions for Hari

1. Accept LA1 to LA5 (section 7) by name for the next diagnostic trial, as decision 6769742939 accepted DA2 to DA4 for the first. Until accepted, each is a deployment assumption without an offline test under docs/agents/factory.md "Item lifecycle" step 3.

## 10. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope: any value, name, member, order rule, header, predicate or literal in sections 3 and 6, any file outside 2.1, or any path in 2.2. In particular: the offline test needs a workflow change; the journal needs a schema version other than 3; a verifier change is needed; or the image test exceeds 900 s with the section 3.7 delays.

## 11. Plan-check questions

- (a) Effort that scales with a number unknown at planning time: verification failures and review findings, capped at 20 root causes each per session with `push -wip and hand off to the next session`. Image runs are fixed at 2 plus 2 mutations.
- (b) Stricter than the parent decisions: the phase reader rechecks `LeaderUp` from the published report bytes (the decision asks only that the child wait); the strict capture-time order of section 3.4; R10's assertion that a positive pair forms offline; LA5. Each turns a decided step into a checked one; none changes a design rule.
- (c) Dependencies neither on `main` nor in the pull request: Event Hubs behaviour (LA1 to LA5), live-only; Docker and image registries on the runner and in the workspace, checked by the test itself. librdkafka statements rest on the pinned v2.16.0 documents and source (section 12).

## 12. Sources

- librdkafka v2.16.0, read 2026-11-11: [INTRODUCTION.md, Sparse connections](https://github.com/confluentinc/librdkafka/blob/v2.16.0/INTRODUCTION.md#sparse-connections); [CONFIGURATION.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md) (`statistics.interval.ms` "The granularity is 1000ms"; `message.timeout.ms` default 300000; `linger.ms` default 5; `socket.connection.setup.timeout.ms` default 30000; `partitioner` `consistent_random`); [STATISTICS.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md) (`brokers.nodeid`, `brokers.state`, `partitions.leader`; partitions keyed by partition id); [src/rdkafka_broker.c](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c) (`rd_kafka_broker_needs_connection` and the producer partition serve path).
- Confluent.Kafka v2.16.0: [DeliveryResult.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/DeliveryResult.cs) (`Partition`, `Offset`, `Status`).
- Code on `main` at `e74849ea`: `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs`, `B10AbruptProducer.cs`, `B10AbruptWire.cs`, `B10AbruptJournal.cs`, `B10AbruptStatistics.cs`; `src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs`; `infra/scripts/phase/b10_abrupt.py`; `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptOfflineImageTests.cs`, `B10AbruptProcessHarness.cs`; `tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs`; `scenarios/B10/scenario.yaml`; `.github/workflows/b10-offline-image.yml`; `.github/CODEOWNERS`.
- Decisions and evidence of section 1.

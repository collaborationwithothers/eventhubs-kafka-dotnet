# B10 warm leader contract-grade plan

Revision 3, issue #404. Short name: `B10 warm leader`. Baseline: `main` at `e74849ea716b6c4b8fa6c5143c6c4175c7038eea`, read 2026-11-11.

This pull request makes the positive restart outcome reachable on Event Hubs. Each child arm first sends one warm-up record to its own partition. It then waits for that record's successful delivery and for a statistics report showing the partition leader `UP`, and only then sends its originals. The restart probe producers use the baseline `message.timeout.ms` 300000 instead of C1's 100; the child also sets `linger.ms` 0. The child gets one 690 s deadline, so every child path ends before the parent's 720 s death wait. The pull request carries the ADR-021 amendment, the LoadGen and phase-reader code, and the offline proof. `docs/blueprint.md` is protected, so Hari merges.

## 0. Revision history

- Revision 1 (`c685b63c`), checked in [6798486853](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6798486853): six blocking findings (B1 to B6), P2-A to P2-E marked apply in implementation, P2-F not marked.
- Revision 2 answers that check:
  - B1: the leader gate now waits, within the existing report budget, for the first report after the warm-up that shows the leader `UP`. Sections 3.1, 3.5, 3.6 and R4 state it the same way.
  - B2: section 3.4 splits the warm-up rules into rules that always apply and rules that apply only once the arm's ordinal-0 `prepared` exists. A synchronous warm-up rejection has no journal record of its own; the `no_kill` record is its trace. Section 6 adds reader tests for each no-kill journal.
  - B3: section 3.2 pins the successor's `capabilities.json` entry for `message.timeout.ms`.
  - B4: section 6 has three image mutations, with the successor setting reverted on its own. Each expected failure is stated as the code would produce it.
  - B5: `B10AbruptEndToEndTests` runs through the same delaying proxy; its assertions are unchanged.
  - B6: answered by [decision 6799006446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6799006446), which accepts LA1 to LA5 by name for the next diagnostic trial (section 7).
  - P2-A: the implementing tool is Codex ([decision 6798381277](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6798381277)).
  - P2-B to P2-E are folded into sections 2.3, 3.1 and 2.1.
  - P2-F: [decision 6800669100](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6800669100) sets a 90 s warm-up delivery bound and a 690 s child deadline. Section 3.8 has the timing table.
- Revision 2 (`fe59fdc0`), checked in [6801044256](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6801044256): one blocking finding (B1, the 30 s exit margin), P2-A to P2-D marked apply in implementation, P2-E not marked.
- Revision 3 records [decision 6801095762](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6801095762) and that check's P2 findings:
  - B1: LA6 is accepted by name and moves to section 7; the section 3.8 and section 7 sentences now say what the offline interval covers.
  - P2-A and P2-C: the rule 7 text in section 3.1 takes the check's exact wording.
  - P2-B: sections 3.5 and R4 pin the leader-report wait before the arm's ordinal-0 `prepared`; section 6 adds the check's reader and producer cases.
  - P2-D: section 3.4 pins the integer ranges of `partition` and `offset`; section 6 adds the check's case.
  - P2-E: section 2.1 allows the two `B10AbruptEndToEndTests` role bounds to rise to 300 s (decision 6801095762 point 2).
  - None of these is design-changing under docs/agents/factory.md "Item lifecycle" step 1; the pointer comment gives the reason for each.

## 1. Goal and authority

Goal: an original can be transmitted, unconfirmed at the kill, then resent by the successor and seen as a cross-restart duplicate. The kill position, the verdict rules and the C1 timeout Job do not change.

Why (evidence, not authority): the trial of run 43010596896 ([6796480692](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6796480692)) and its diagnosis ([6796659976](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6796659976)) found the following. Every abrupt producer copies the resolved C1 settings (`src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs` lines 191 to 193), so it ran with `message.timeout.ms` 100. The successor's resends expired unsent before their cold leader connection came up. The cross-restart originals were almost certainly never transmitted before the kill.

Authority. The item state file `items/404/state.md` on `main` lists the decisions in force up to comment 6790675342. New since then, in creation order:

- [6794365032](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6794365032): retroactive acceptance of the `LocalKafkaFixture.cs` change in pull request 432; "it does not widen the plan further".
- [6797472633](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797472633), option A, to continue. Each arm's partition leader connection is open before its originals. The restart probe producers (child and successor) get a delivery timeout that covers connection set-up. The C1 timeout Job keeps its own settings. This changes ADR-021, and "a further trial needs Hari's decision".
- [6797974611](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797974611), "A B A":
  - Q1 A: one warm-up record per child arm with the arm's own key. The child waits for its successful delivery report, then "for a statistics report that shows that partition's leader broker in state `UP`". Warm-up records are their own journal kind and cohort.
  - Q2 B: the kill stays immediately after the cut; the child's probe producer sets `linger.ms` 0; the successor may keep the baseline.
  - Q3 A: the probe producers use the baseline `message.timeout.ms` 300000; idempotence and `max.in.flight` 5 stay as in C1; the C1 timeout Job is unchanged; no successor warm-up.
- [6797990874](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797990874): one pull request carries the ADR-021 amendment, the code and the offline proof. Hari merges.
- [6798381277](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6798381277): the implementing tool is Codex.
- [6799006446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6799006446): Hari accepts LA1 to LA5 by name, for the next B10 diagnostic trial only.
- [6800669100](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6800669100):
  - Point 1: each arm's warm-up delivery wait is bounded by 90 s, fixed policy, not measured. The value is the documented `socket.connection.setup.timeout.ms` default 30000 plus the baseline `request.timeout.ms` 60000. Past that, the arm ends on the no-kill path with `b10_abrupt_warmup_failed`.
  - Point 2: the child has one deadline 690 s after its start: the parent's 720 s death wait less a 30 s margin for exit. On the no-kill path the final flush is bounded by the time remaining before that deadline, not a fresh 300 s.
- [6801095762](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6801095762):
  - Point 1: Hari accepts LA6 by name, for the next B10 diagnostic trial only (section 7). Option not taken: starting the child's deadline from the parent's start.
  - Point 2: the parent-role bound (210 s) and the successor bound (160 s) in `B10AbruptEndToEndTests` may rise to 300 s each. This is an approved bound change, not a loosened assertion.
  - Point 3: P2-A to P2-D of check 6801044256 are folded into the plan text as the check gives them.

Decisions in force that this plan keeps:
- Schema 3, the 120 s release, the control drain bound, the sticky torn-record gap and no-kill exit 3 ([6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785)).
- `CutOrdinal` 4 ([6769269811](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769269811)) and `ProbeRate` 100 ([6769312013](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769312013)).
- The no-kill end ([6768228960](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768228960)).
- The offline test's mechanism and accepted residuals ([6787880185](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787880185), [6788215099](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788215099), [6788464379](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788464379)).
- No verifier change ([6787849492](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787849492)).

Design text changed:
- docs/blueprint.md ADR-021 protocol rules 1, 3, 5, 6, 7, 8 and 15, and its "Classification" bullet (section 3.1).
- ADR-021 rule 15 freezes the pre-acceptance statistics gate after the trial. Decision 6797974611 is Hari's design decision that changes that gate. Decision 6800669100 adds the child deadline. The amendment records both.

Implementing tool: Codex (6798381277).

## 2. Scope

### 2.1 Files allowed to change

1. `docs/blueprint.md`:
   - ADR-021 (section 3.1).
   - The amendment-index paragraph in the preamble before section 1, at line 12 at baseline, which starts "The B10 restart redesign amends ADR-012, ...".
2. `docs/operations/b10-abrupt-contract.md`: the settings, journal kinds, probe sequence, deadline and failure names of section 3.
3. `src/Parchwell.LoadGen/Run/Abrupt/*.cs`.
4. `infra/scripts/phase/b10_abrupt.py`.
5. `infra/scripts/phase/tests/test_b10_abrupt.py`.
6. `tests/Parchwell.LoadGen.Tests/Abrupt/*.cs`, existing and new files.
   - Existing tests may change their fixture journals, fake producers and fake statistics to supply the records this plan requires.
   - `B10AbruptEndToEndTests.cs` may route its broker through the section 3.7 proxy, and may raise its parent-role bound (210 s, line 38 at baseline) and its successor bound (160 s, line 77) to 300 s each (decision 6801095762 point 2). No other bound or assertion in it changes.
   - No existing assertion is loosened, skipped or deleted.
7. `tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs`: one optional advertised-address input (section 3.7). Existing callers keep their behaviour.
8. `tests/Parchwell.ArchitectureTests/B10AbruptFailureSiteTests.cs`, only if the new failure names require it.
9. `items/404/state.md` and new `items/404/b10-warm-leader-receipt.md`. The implementing session writes both (docs/agents/factory.md "Item state file").

### 2.2 Must not change

- `scenarios/**`. C1 keeps `message.timeout.ms` 100 and `max.in.flight` 5 for the C1 timeout Job.
- The ordinary LoadGen host and every file under `src/` outside `Run/Abrupt/`. That includes `src/Parchwell.Kafka/**`: `BaselineSettingsCatalog.cs` keeps 300000, 5 and 15000.
- `src/Parchwell.Verifier*/**` and `tests/Parchwell.Verifier*/**` (decision 6787849492).
- `.github/**`, including `b10-offline-image.yml` and its `--filter-method` test name.
- `charts/**`; `infra/**` other than items 4 and 5; `scripts/**`.
- `results/**`; any `expected.yaml`.
- `*.csproj`, `Directory.*.props`, `*.sln*`, lock files.
- `AGENTS.md`, `CLAUDE.md`, and every other file under `docs/`.
- No new project, assembly reference, NuGet package or tool.
- The parent's 720 s death wait (`B10AbruptParent.cs` line 63) and the 900 s watchdog (`B10AbruptHost.cs` lines 57 to 58).
- Test names and exports read from outside these files:
  - The offline test method name `Given_BuiltImage_When_ParentChildAndSuccessorRunInContainers_Then_JournalExitAndResendsAreObserved`.
  - The class `Parchwell.LoadGen.Tests.Abrupt.B10AbruptWireProducerTests`, its exported file set and its completion within 60 s with `PARCHWELL_B10_ABRUPT_WIRE_OUTPUT` set. The protected `tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs` reads all three.

### 2.3 Required CI and protected paths

- `.NET verification` (required).
  - `scripts/verify-dotnet.sh` builds with warnings as errors and runs every selected unit project (lines 93 to 100 at baseline), including `tests/Parchwell.LoadGen.Tests`, `tests/Parchwell.ArchitectureTests` and `tests/Parchwell.Verifier.Tests`.
  - The last one runs `B10AbruptWireProducerTests` as a subprocess (section 2.2).
  - The offline image test returns early there because `PARCHWELL_B10_OFFLINE_IMAGE` is unset.
- `Lifecycle verification`.
  - "Verify lifecycle with stub commands" runs `infra/scripts/phase/tests` through `infra/scripts/tests/test_phase_bridge.py`.
  - "Verify the load generator accepts the rendered chart environment" runs `charts/parchwell/tests/check-loadgen-host.sh`, which runs `infra/scripts/phase/tests/check_b10_abrupt_producers.py`.
- `docs-advisory`: advisory documentation checks.
- Not reached: `verifier-c5-mutation.yml` (no verifier change).
- `b10-offline-image.yml` has only a `workflow_dispatch` trigger; Hari runs it on `main` after merge.
- Protected path touched: `docs/blueprint.md` (`/docs/` in `.github/CODEOWNERS`). `docs/operations/` is listed there with no owner. Hari merges.

## 3. External contracts

### 3.1 ADR-021 amendment text

The implementer writes these statements into the named rules of ADR-021 and keeps every other sentence. Wording may be adjusted to fit the surrounding sentence; the stated facts, values, sources and failure names may not change.

- **Protocol rule 1**, after "Both arms use equal configured payload shape, settings, rate and cut ordinal.":
  - "Before each arm's first original, the child produces one warm-up record with that arm's key and payload bytes, its own attempt ID and sequence -1. It waits, at most 90 s, for its successful delivery report."
  - "librdkafka connects only to brokers it needs, including leaders for partitions being produced to ([Sparse connections, INTRODUCTION.md v2.16.0](https://github.com/confluentinc/librdkafka/blob/v2.16.0/INTRODUCTION.md#sparse-connections)). That a producer opens a leader connection only once a message is queued for that leader is inferred from [rdkafka_broker.c v2.16.0](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c) (debug text "message(s) queued but broker not up"); it is not documented."
  - "The `consistent_random` partitioner sends equal keys to one partition ([CONFIGURATION.md v2.16.0](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md), `partitioner`: CRC32 hash of key)."
  - "Warm-up records are their own journal kinds and their own `warmup` cohort. They are authorized attempts of the first restart instance, and they never enter timeout totals, arm counts, unconfirmed sets, resends or pairs."
  - "The first restart instance's producer and the successor's producer use C1's settings except `message.timeout.ms` 300000, the baseline value. That exceeds the documented default maximum for one broker connection set-up, `socket.connection.setup.timeout.ms` 30000 (CONFIGURATION.md v2.16.0). That it covers connection set-up on Event Hubs is an inference, accepted by Hari as a live-only assumption ([decision 6799006446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6799006446))."
  - "The child's producer also sets `linger.ms` 0; the successor keeps the baseline `linger.ms` 5. The C1 timeout Job keeps C1's settings. The 90 s bound is the documented `socket.connection.setup.timeout.ms` default plus the baseline `request.timeout.ms` 60000. These values are policy ([decision 6797974611](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797974611), [decision 6800669100](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6800669100)), not measurements."
- **Protocol rule 3**:
  - In the list of records the producing thread appends, add "`warmup_prepared` before the warm-up invocation".
  - After the callback sentence, add "The warm-up delivery callback enqueues a `warmup_terminal` record through the same queue."
  - Add "A warm-up record is not a probe attempt: it is never `unreported_seen` or `unreported_unseen`."
- **Protocol rule 5**, replacing its first sentence:
  - "For each arm, after its durable `warmup_terminal` record and within the existing statistics wait, the child waits for the first parsed statistics report captured after that record in which the warm-up's delivered partition has a leader broker in state `UP`. That means `topics.<topic>.partitions.<partition>.leader` equals the `nodeid` of exactly one entry of `brokers`, and that entry's `state` is `UP` ([STATISTICS.md v2.16.0](https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md))."
  - "The child journals a binding of that report (name, length, SHA-256, sequence, time, warm-up attempt ID and partition) before its first original's acceptance."
  - "A sampled `UP` shows the connection was up when librdkafka built the report, not at any later instant."
  - Keep "No post-acceptance report gate; `statistics.interval.ms` stays unchanged."
- **Protocol rule 6**, after the kill unconfirmed set sentence: "Warm-up records belong to no unconfirmed set."
- **Protocol rule 7**, after "No-kill path:":
  - "The child has one deadline, 690 s after its `child_started` record: the parent's 720 s death wait less a 30 s margin for exit, fixed policy ([decision 6800669100](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6800669100)). Every child wait before `kill_intent` is bounded by the time left before that deadline."
  - "A warm-up not delivered within 90 s, or before the deadline, is `b10_abrupt_warmup_failed`."
  - "No report captured after the warm-up within the statistics wait is `b10_abrupt_sample_missing`."
  - "Reports captured after the warm-up, none of which shows the leader `UP` within that wait, is `b10_abrupt_leader_not_up`."
  - "Reaching the deadline in any other wait before `kill_intent` is `b10_abrupt_deadline_exhausted`."
  - "Each ends the child on the no-kill path. The pre-admission `Flush` there is bounded by the time left before the deadline, not by `ProducerDrainTimeout`."
  - "A cross-restart warm-up does not admit the arm's first original."
  - In the existing rule 7 sentence "If the cross-restart arm's first original has not been admitted, the child then calls the producer's `Flush` within the existing `ProducerDrainTimeout`, disposes the producer and exits with exit code 3", replace "within the existing `ProducerDrainTimeout`" with "within the time left before the child deadline", so the rule states one bound. The contract page's matching phrase "flush within the existing producer-drain budget" becomes "flush within the time left before the child deadline".
  - The kill immediately after the durable `kill_intent` is unchanged.
- **Protocol rule 8**, after the resend sentence: "The successor sends no warm-up. Under rule 1's settings its resends wait for its leader connection instead of expiring at C1's 100 ms."
- **"Classification" bullet**, after its first sentence: "Consumed warm-up records join their `warmup_prepared` journal records and form the separate `warmup` cohort; they enter no probe or timeout count."
- **Protocol rule 15**, a new final paragraph:
  - "Amendment of 2026-11-11 ([decision 6797472633](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797472633), [decision 6797974611](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797974611), [decision 6797990874](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797990874), [decision 6800669100](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6800669100))."
  - "The diagnostic trial of run 43010596896 found that the probe producers inherited C1's `message.timeout.ms` 100, so the successor's resends expired before their leader connection came up ([diagnosis 6796659976](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6796659976))."
  - "Rules 1, 3 and 5 to 8 add the per-arm warm-up, the leader-readiness binding, the probe settings and the child deadline. This is Hari's design change to the pre-acceptance statistics gate, outside the mechanical changes this rule otherwise allows. The eligibility threshold, control gate, verdict rules, restart subject and expectations are unchanged."
  - "The kill stays immediately after the cut. The child cannot observe whether an original is on the wire at the kill: librdkafka documents no per-message transmission signal, and statistics granularity is 1000 ms (`statistics.interval.ms`, CONFIGURATION.md v2.16.0). The B10 baseline emits a report every 15000 ms (`src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs`). Transmission is observed only when the verifier consumes the original."
  - "Hari accepted by name, for the next diagnostic trial only, five live-only assumptions ([decision 6799006446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6799006446)): Event Hubs connection set-up stays inside the timeout; the leader and its connection do not change between warm-up and originals; the produce round trip leaves at least one original in flight at the kill and a nonempty control set at the cut; Event Hubs appends a fully sent request after the client process dies; and statistics give the partition a leader matching one broker entry. A further trial needs Hari's recorded decision."
- **Amendment-index paragraph** (preamble, line 12 at baseline), one added sentence: "The B10 warm-leader change amends ADR-021 protocol rules 1, 3, 5 to 8 and 15 and its classification bullet under [decision 6797472633](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797472633), [decision 6797974611](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797974611), [decision 6797990874](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6797990874) and [decision 6800669100](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6800669100); Hari's merge of its pull request records the amendment."

### 3.2 Probe producer settings and their evidence

| Producer | Effective settings |
| --- | --- |
| C1 timeout Job | Unchanged: resolved C1 `LoadGenProducer` (`message.timeout.ms` "100", `max.in.flight` "5", `enable.idempotence` "true", baseline otherwise) |
| Restart child | Resolved C1 `LoadGenProducer`, then `message.timeout.ms` = "300000" and `linger.ms` = "0" |
| Successor | Resolved C1 `LoadGenProducer`, then `message.timeout.ms` = "300000"; `linger.ms` stays baseline "5" |

These values are constants in the abrupt code. They are not new `B10Abrupt__` keys and not values in `scenarios/B10/scenario.yaml`.

Evidence of the settings:

- The child's `B10AbruptStatistics` expected-settings dictionary holds the effective child values, including `linger.ms` "0". The child writes no `capabilities.json`.
- The successor's resolved configuration, as written to `capabilities.json` by `RunEvidenceSession` and read back by `RunEvidenceReader`, gives `message.timeout.ms` the entry `value` "300000", baseline metadata unchanged, `override` null. That is true: the probe producers do not apply C1's override of this key, so the effective value equals the baseline. `ValidateResolvedSetting` already accepts an unoverridden value equal to its baseline.
- The successor's expected resolved configuration (the `selected` side of that reader) is built the same way, so the reader's selected value is "300000".
- `max.in.flight` and `enable.idempotence` keep their C1 values and their scenario `override` provenance.
- Every other key is unchanged.

### 3.3 Warm-up record on the wire

- Topic: `document-events` (the existing `options.Topic`).
- Key: the arm key `B10/{run_id}/C1/1/{arm}`.
- Value: the arm's probe payload bytes.
- Headers, in this order: `instance-id` = instance ID; `attempt-id` = `{instance_id}-{arm}-warmup`; `sequence` = `-1`.
- The caller sets no partition; the configured partitioner places the record.

### 3.4 Journal (schema 3, kinds added)

`schema_version` stays 3. Closed member lists:

| Kind | Writer | `data` members |
| --- | --- | --- |
| `warmup_prepared` | Producing thread, before the warm-up `Produce` | `attempt_id`, `arm`, `key`, `payload_length`, `payload_sha256` |
| `warmup_terminal` | Journal writer thread, from the warm-up delivery callback through the existing bounded queue | `attempt_id`, `completion_source` (`callback`), `delivered`, `error_code`, `persistence_status`, `partition`, `offset` |
| `pre_acceptance_report` | Unchanged position | Existing members plus `warmup_attempt_id`, `partition` |

- `post_drain_report` keeps its existing members.
- `warmup_prepared` is a producing kind; `warmup_terminal` is not.
- `partition` is a JSON integer in the signed 32-bit range and `offset` a JSON integer in the signed 64-bit range (Confluent.Kafka v2.16.0: `Partition.Value` is `int`, `Offset.Value` is `long`). C# reads them with `TryGetInt32` and `TryGetInt64`; Python checks `-2147483648 <= v <= 2147483647` and `-9223372036854775808 <= v <= 9223372036854775807`. The existing 32-bit `Integer` helper is not used for `offset`. When `delivered` is true, both are at least 0.
- A synchronous warm-up rejection (`Produce` throws) has no record of its own. The `no_kill` record with reason `b10_abrupt_warmup_failed` is its trace.

Rules every reader enforces, in two groups.

Always (any journal, including no-kill journals):

- At most one `warmup_prepared` per arm.
- `attempt_id` is `{instance_id}-{arm}-warmup` and `key` is the arm key.
- The cross-restart `warmup_prepared` comes only after the control arm is complete, under the existing control-completeness condition for cross-restart `prepared`.
- At most one `warmup_terminal` per `warmup_prepared`, after it, with a matching `attempt_id`.
- A `warmup_terminal` may follow `no_kill`: a flush on the no-kill path can complete it late.

Only when that arm's ordinal-0 `prepared` exists:

- Exactly one `warmup_prepared` and exactly one `warmup_terminal` for the arm both precede that `prepared`.
- The terminal has `delivered` true and `error_code` 0, with `persistence_status` in the set the existing reader already permits for error code 0 (0 or 2).
- The arm's `pre_acceptance_report` has `warmup_attempt_id` equal to the warm-up's attempt ID, `partition` equal to the terminal's partition, and `report_capture_utc` strictly later than the terminal's `observed_utc`.

A journal that ends on `no_kill` before an arm's ordinal-0 `prepared` gets no gap or failure from the second group, whatever that arm's warm-up records show.

### 3.5 Leader-readiness predicate `LeaderUp(report, topic, partition)`

`LeaderUp` reads the raw statistics JSON. It is true only when all of these hold:
- `topics[topic].partitions[decimal string of partition].leader` is an integer L, with L at least 0;
- exactly one value in `brokers` has `nodeid` equal to L;
- that value's `state` is the string `UP`.

Anything else is false: a missing topic, partition or field; L of -1; no matching broker or two; any other state, for example `INIT`, `CONNECT` or `DOWN`.

Order in the child: the warm-up and this report selection both happen before the arm's ordinal-0 `prepared`. The selected report is bound after that `prepared` and before its `accepted` (R4). So a `b10_abrupt_leader_not_up` or `b10_abrupt_sample_missing` end leaves no `prepared` without `accepted`, which the existing reader would mark `malformed_frame`.

Selection in the child: among parsed reports captured strictly after the arm's `warmup_terminal` `observed_utc`, take the first for which `LeaderUp` is true. The wait is bounded by the existing statistics wait (`StatisticsCompletionTimeout` 20 s in the abrupt host, or `CleanupTimeout` when unset) and by the child deadline. The phase reader applies `LeaderUp` again to the published report bytes.

### 3.6 Failure names

The no-kill reasons gain two names: in C# `B10AbruptWire.NoKillReasons`, in the phase reader's accepted names and on the contract page.
- `b10_abrupt_warmup_failed`: the warm-up `Produce` threw, or its terminal had `delivered` false or a non-zero error code, or no `warmup_terminal` was durable within 90 s or before the deadline.
- `b10_abrupt_leader_not_up`: at least one report was captured after the warm-up within the wait, but none satisfied `LeaderUp`.

Existing names keep their meaning:
- `b10_abrupt_sample_missing`: no report was captured after the warm-up within the wait.
- `b10_abrupt_deadline_exhausted`: the deadline was reached in any other wait before `kill_intent`.

Reader outcomes:
- A breach of section 3.4's rules is the existing capture gap `malformed_frame`. That is checked in the "always" group, and in the second group once the ordinal-0 `prepared` exists.
- A `pre_acceptance_report` whose warm-up fields, capture time or `LeaderUp` fail is the existing `b10_abrupt_report_binding_invalid`.
- The phase reader rejects such a first publication through its existing report-binding failure path, so the successor is not released.

### 3.7 Offline proxy and offline image test

The TCP proxy lives in a test file under `tests/Parchwell.LoadGen.Tests/Abrupt/`.
- It listens on `127.0.0.1` at an ephemeral port.
- `LocalKafkaFixture` gains an optional input that makes the broker's `PLAINTEXT` advertised listener `127.0.0.1:<proxy port>`. Without that input the fixture behaves as today.
- Every Kafka client of a test that uses the proxy bootstraps at the proxy.

Proxy behaviour, per accepted connection:
- It waits `ConnectDelay` = 500 ms before opening its upstream connection to the broker.
- Client-to-broker bytes are forwarded with no added delay.
- Each broker-to-client chunk is delivered no earlier than `ResponseDelay` = 500 ms after the proxy read it, in order.
- When the client side closes, bytes already read from the client are still forwarded upstream before the upstream side closes.
- It counts accepted connections.

Both delays are test policy, not Event Hubs measurements.

The offline image test keeps the same method and class. Additions:
- Every client (admin, the parent's child, the successor and the test consumer) uses the proxy.
- The consumer's read window becomes 60 s, from 20 s.
- The 900 s test bound, the container settings and every existing assertion stay.
- The observation line keeps its keys and their order. After `consumed_resends` it appends: ` warmup_records=<count of warmup_terminal records> kill_originals_consumed=<count> parent_registered_to_child_started_microseconds=<decimal> connect_delay_ms=500 response_delay_ms=500`. The new interval is taken from the parent's `parent_registered` and `child_started` stage ticks.

`B10AbruptEndToEndTests` uses the same proxy with the same two delays, so a local broker cannot acknowledge control originals before the `cut`. Its assertions are unchanged.

### 3.8 Child timing (decision 6800669100)

The deadline is 690 s after the child's `child_started` record, measured on the child's monotonic clock. Bounds used:
- statistics waits 20 s each;
- warm-up delivery 90 s per arm;
- release 120 s;
- control drain `ProducerDrainTimeout` 300 s;
- no-kill dispose `CleanupTimeout` 10 s.

Each wait before `kill_intent` uses the smaller of its own bound and the time left before the deadline. Worst cases, by script, in seconds after `child_started`:

| Path | Worst time the path is taken | Then | Worst end |
| --- | --- | --- | --- |
| Success: `kill_intent`, then self-kill | 680 | SIGKILL | 680 |
| No initial report | 20 | Flush within time left, dispose within 10 s, exit 3 | 700 |
| Control warm-up not delivered | 110 | Same | 700 |
| Control report missing or leader not `UP` | 130 | Same | 700 |
| Control drain incomplete | 550 | Same | 700 |
| Post-drain report missing | 570 | Same | 700 |
| Cross-restart warm-up not delivered | 660 | Same | 700 |
| Cross-restart report missing or leader not `UP` | 680 | Same | 700 |
| Failure after cross-restart admission | 680 | Exit 3, no flush or dispose | 680 |
| Deadline reached in any wait before `kill_intent` | 690 | Flush gets 0 s, dispose within 10 s, exit 3 | 700 |

For comparison, by the same script:
- On `main` today, the worst success path is 460 s, and drain incomplete followed by a 300 s flush reaches 740 s.
- In Revision 1, the worst success path was 1100 s, and a failed cross-restart warm-up followed by its flush reached 850 s.

Every end above is before 720 s on the child's clock. The parent's 720 s runs from the parent's own start (`B10AbruptParent.cs` line 51 at baseline), which is earlier than `child_started`. The 30 s margin must therefore also hold:
- the parent's work before the child starts;
- the child's runtime start before `child_started`;
- process exit.

The 10 s dispose is outside the 690 s and inside the margin. The other parts are LA6, accepted by name (decision 6801095762, section 7). One AKS reading supports it (trial run 43010596896, from that decision): the parent marked `child_started` 627 ms after its start and the child's own `child_started` record came 557 ms later on the shared monotonic clock, 1184 ms in total by script. The offline test's `parent_registered_to_child_started_microseconds` (section 3.7) covers only the parent's registration to its `Process.Start` mark (`B10AbruptParent.cs` line 62), on a GitHub runner; it does not cover parent start to registration or the child's start to its `child_started` record. A total that crosses 720 s ends as unknown termination, as today.

## 4. Requirements

- R1. WHEN the restart child or the successor constructs its producer THEN its configuration has the section 3.2 values, and the settings guard and the successor's `capabilities.json` record them as section 3.2 says. WHEN the C1 timeout Job runs THEN nothing about it changes.
- R2. WHEN the control arm starts (after the existing initial report wait) or the cross-restart arm starts (after the control arm is complete) THEN, before the arm's ordinal-0 `prepared`, the child appends a durable `warmup_prepared` and calls `Produce` once with the section 3.3 record.
- R3. WHEN the warm-up delivery callback runs THEN it only enqueues `warmup_terminal`, with no file or network I/O.
  - The producing thread waits until that record is durable, at most 90 s and at most until the deadline.
  - It continues only when `delivered` is true and `error_code` is 0.
  - Otherwise, or when `Produce` throws, or when either bound runs out, the child takes the no-kill path with `b10_abrupt_warmup_failed`.
- R4. WHEN the warm-up has succeeded THEN, before the arm's ordinal-0 `prepared`, the child selects a report as section 3.5 says.
  - No report after the warm-up within the wait gives `b10_abrupt_sample_missing`.
  - Reports, none `UP`, give `b10_abrupt_leader_not_up`.
  - The selected report is the arm's pre-acceptance report. It is bound after ordinal 0's `prepared` and before its `accepted`, with the section 3.4 members.
- R5. WHEN either arm's warm-up or leader check fails, or the deadline is reached before `kill_intent`, THEN the no-kill rules apply with `cross_restart_admitted` false. The producer is flushed within the time left before the deadline, disposed within `CleanupTimeout`, and the child exits 3. The warm-up never sets cross-restart admission. After cross-restart admission, the existing exit 3 without flush or dispose stays.
- R6. WHEN the originals run THEN these are unchanged: pacing, ordinals, the cut, `kill_intent`, the self-kill immediately after it, and the ban on `Flush` and `Dispose` after the first cross-restart original's admission.
- R7. WHEN the successor runs THEN it sends no warm-up. Its resends are unchanged except for R1's settings.
- R8. WHEN `B10AbruptJournal.Read` reads a journal THEN it enforces sections 3.4 and 3.6. The control and kill unconfirmed sets are computed from `prepared` originals only, as today.
- R9. WHEN the phase reader accepts a first publication THEN it:
  - accepts the new kinds, members and failure names;
  - enforces sections 3.4 to 3.6, including `LeaderUp` on the published `<arm>-pre-acceptance-report.json` bytes;
  - otherwise rejects as section 3.6 says.
- R10. WHEN the offline image test runs THEN, besides its existing assertions, it asserts:
  - for each arm, the section 3.4 order with `delivered` true;
  - each bound pre-acceptance report satisfies `LeaderUp`;
  - no `terminal` record in the child journal, and no successor ledger row, has `error_code` -192;
  - at least one kill-set original was consumed with instance ID `first`, and its linked successor resend was consumed at a different offset, with equal key, `sequence` header and payload hash;
  - the proxy accepted at least one connection while each role ran.
- R11. WHEN the pull request merges THEN ADR-021 and the contract page carry sections 3.1 to 3.8. Each rule is stated once, in the file that owns it, and the other files cite it.

## 5. Invariants and safety rules

- No Azure credential and no Azure call. No workflow dispatched by an agent. No trial: a further trial needs Hari's decision (6797472633).
- These do not change: the C1 timeout Job, C0, the scenario file, the verdict rules, eligibility, the restart subject and the expected outcomes.
- Callbacks still do no file or network I/O. The journal stays create-new, flushed per record and within its existing limits.
- No number from the offline tests is written as an Event Hubs, AKS or librdkafka fact (AGENTS.md hard safety rule 11). The 500 ms delays are test policy. The 90 s and 690 s values are Hari's policy.
- Warm-up records never enter any unconfirmed set, resend, pair or count.

## 6. Test map

Common inputs:
- run ID `b10d-1-1`, instance `first`, topic `document-events`;
- statistics fixtures are minimal raw JSON objects with `topics` and `brokers` as section 3.5 reads them, inside reports that also pass the existing guards;
- deadline and wait tests use the existing test clock.

Tests are in LoadGen unless marked Python.

| Requirement | Test | Inputs and literal expectations |
| --- | --- | --- |
| R1 | Settings test on the abrupt host's producer configuration for roles `child` and `successor` | Child: `message.timeout.ms` "300000", `linger.ms` "0", `enable.idempotence` "true", `max.in.flight` "5". Successor: "300000", `linger.ms` "5". Existing `B10AbruptHostRegistrationTests` still asserts the scenario role value "100". |
| R1 | Successor capabilities round-trip through `RunEvidenceReader` | `message.timeout.ms` entry: `value` "300000", `override` null, accepted. `max.in.flight` entry: `value` "5", `override` with `scenario_id` "B10" and `configuration_id` "C1". A hand-built entry with `value` "300000" and the C1 `override` is rejected by the reader. |
| R2, R4 | Producer test, fake producer delivering every record | Journal kind order per arm: `warmup_prepared`, `warmup_terminal`, `prepared` (ordinal 0), `pre_acceptance_report` with `warmup_attempt_id` `first-same_instance-warmup` and the fake's partition, `accepted`. Warm-up message headers: `instance-id` "first", `attempt-id` `first-same_instance-warmup`, `sequence` "-1"; key equal to the arm key. |
| R3 | Producer tests, four cases | `Produce` throws on the warm-up; warm-up delivered with error -192; warm-up never delivered with the clock advanced 90 s; warm-up never delivered with the deadline reached first. Each gives `no_kill` reason `b10_abrupt_warmup_failed` (section 3.6: the deadline reached during the warm-up wait is a warm-up failure, not `b10_abrupt_deadline_exhausted`), `cross_restart_admitted` false, and no ordinal-0 `prepared` for that arm. A cross-restart warm-up failure after a complete control arm gives `b10_abrupt_warmup_failed` and exit 3. |
| R4 | Producer tests | One report after the warm-up with leader state `INIT`, then none: `b10_abrupt_leader_not_up`, and the journal has no ordinal-0 `prepared` for that arm. One `INIT` report, then one `UP` report within the wait: proceeds and binds the `UP` report. No report within the wait: `b10_abrupt_sample_missing`. A report captured before `warmup_terminal`'s time is never selected. |
| R3 | Callback test | The warm-up callback performs no journal write on the callback thread. The record arrives through the queue (the existing callback-isolation pattern). |
| R5 | Deadline tests with the test clock | Control drain incomplete with 140 s left before the deadline: the no-kill flush is bounded by 140 s, not 300 s. Deadline reached during the release wait: `b10_abrupt_deadline_exhausted`, flush bound 0 s, exit 3. Failure after cross-restart admission: exit 3 with no `Flush` or `Dispose` call. |
| R5, R6 | Existing producer and end-to-end tests, adapted | Unchanged assertions on cut, `kill_intent`, self-kill, nonempty control and kill sets, and no-kill exits pass with warm-ups present. `B10AbruptEndToEndTests` runs through the section 3.7 proxy, with its parent-role and successor bounds at most 300 s each (section 2.1). |
| R7 | Successor test | No `warmup_prepared` and no warm-up message from the successor; its invocations equal the kill set. |
| 3.5 | `LeaderUp` table test, C# and Python with the same cases | True: partition "0", leader 1, one broker nodeid 1 `UP`. False: state `INIT`; `CONNECT`; `DOWN`; leader -1; partition missing; topic missing; leader 1 with two brokers nodeid 1, both `UP`; leader 1 with only nodeid 2 `UP`; `leader` not an integer. |
| R8 | Journal reader tests on valid and mutated journals | Expectations follow this table. |
| R9 | Python `test_b10_abrupt.py`: the R8 journal cases plus report bytes | Same outcomes, through the phase reader's rejection path for journals with `kill_intent`. A publication whose report bytes fail `LeaderUp` is rejected. The two new names are accepted no-kill reasons. |
| R10 | Offline image test, by hand and by Hari's workflow run | Section 3.7 literals; existing assertions unchanged. |
| R11 | `npm run check:docs`; cold review | Rule texts present once; links resolve. |

R8 journal reader expectations:
- Valid journal with both warm-ups: no gaps, no failures.
- Unconfirmed sets of the valid journal equal those computed with the warm-up records removed.
- These give `malformed_frame`: missing cross-restart `warmup_prepared` before its ordinal 0; `warmup_terminal` after ordinal-0 `prepared`; two control `warmup_prepared`; terminal `delivered` false while that arm's ordinal 0 exists.
- These give `b10_abrupt_report_binding_invalid`: report `warmup_attempt_id` mismatch; report `partition` mismatch; report capture time equal to the terminal's time. One tick later (100 ns, the `O` format's resolution) is accepted.
- These no-kill journals give no gap and no failure:
  - control warm-up with `delivered` false, then `no_kill`;
  - control `warmup_prepared` with no terminal, then `no_kill` (synchronous rejection or bound);
  - the same, with a late `warmup_terminal` after `no_kill`;
  - complete control arm, cross-restart warm-up failed, then `no_kill`;
  - complete control arm, then `no_kill` with no cross-restart warm-up;
  - control `warmup_prepared`, `warmup_terminal` with `delivered` true and `error_code` 0, no ordinal-0 `prepared`, then `no_kill` with reason `b10_abrupt_leader_not_up`;
  - the same with reason `b10_abrupt_sample_missing`.
- A delivered warm-up terminal with `offset` 4294967296 and `partition` 0 is accepted.

Image mutations. Each is applied once in a scratch worktree; the image is rebuilt and the test run once. Each is expected to fail. The count is fixed at 3, so no cap is needed.

| Mutation | What the code would do | Expected failing assertion |
| --- | --- | --- |
| M1. Successor only: `message.timeout.ms` set back to "100" | The successor's resends meet the 500 ms connect and response delays and expire (`-192`, permitted by `IsPermittedTerminal`), so the successor completes with -192 ledger rows | R10's "no successor ledger row has `error_code` -192", or the existing `successor_attempt_not_consumed` |
| M2. Child only: `message.timeout.ms` set back to "100" | The control warm-up expires behind the 500 ms connect delay; the child ends with `no_kill` `b10_abrupt_warmup_failed` and exit 3 | The existing exit-code assertion, `b10_offline_image_exit_code:3` |
| M3. The child skips the warm-up and binds the earlier report as on `main` | The journal reader finds an ordinal-0 `prepared` with no warm-up: `malformed_frame` | The existing `capture_gaps` empty assertion, or `successor_permitted` true |

The predicate map of docs/agents/pull-requests.md covers the journal and phase reader predicates above; no verifier predicate changes.

## 7. Deployment assumptions

Live-only. [Decision 6799006446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6799006446) accepts each by name, for the next B10 diagnostic trial only:

- LA1. Event Hubs leader connection set-up stays inside the probe delivery timeout (300000 ms) and the existing producer drain bound. Under decision 6800669100 the warm-up delivery bound of 90 s also applies. A slower warm-up ends on the no-kill path; that is a known end, not unknown termination.
- LA2. The partition leader, and its connection, do not change between the warm-up's delivery and the arm's originals.
- LA3. The Event Hubs produce round trip is long enough that at least one original is on the wire, unacknowledged, at the kill, and that the control arm's set at its cut is nonempty.
- LA4. Event Hubs appends a fully sent produce request after the client process dies.
- LA5. On Event Hubs, librdkafka statistics give the warm-up's partition a leader that matches the `nodeid` of one broker entry.

The offline proof runs section 3.7 against a local Kafka broker, not Event Hubs:
- the warm-up waits out delayed connections and the bound report shows the leader `UP` (R10, M3);
- the successor's resends survive delayed connections and responses (R10, M1);
- the child's probes survive them too (M2);
- an original sent before the kill is appended and consumed alongside its resend (R10).

The offline test's accepted residuals still apply: DA-i to DA-iii and the resource-request omission (decisions 6787880185, 6788215099, 6788464379).

- LA6. The parent's work before the child starts, the child's start before `child_started`, the child's dispose (at most 10 s) and process exit together take less than 30 s on AKS. Accepted by name for the next B10 diagnostic trial only by [decision 6801095762](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6801095762), point 1, with one AKS reading of about 1.2 s from parent start to the child's `child_started` (section 3.8). The offline test records only the parent's registration-to-spawn part, on a runner, so it does not prove LA6.

The 30 s exit margin itself is Hari's policy (6800669100).

## 8. Acceptance commands

Run by the implementer before the cold review, in its Linux workspace:

1. `./scripts/verify-dotnet.sh`: exit 0, warnings as errors, no skipped test. `Parchwell.Verifier.Tests` passes with `B10AbruptRejectionTests.cs` unchanged; the receipt records that.
2. `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'`: exit 0.
3. `bash charts/parchwell/tests/check-loadgen-host.sh`: exit 0.
4. `npm run check:docs`: each warning in a changed file fixed or explained under docs/agents/pull-requests.md rule 8.
5. The offline image test, by the build and run steps of `.github/workflows/b10-offline-image.yml`, run by hand with Docker:
   - 2 runs, a fixed count. Each must pass and print one section 3.7 line, recorded in the receipt.
   - Then M1, M2 and M3, once each.
   - A failure is a finding for the receipt. The implementer never changes an assertion to make a run pass.

Caps per implementing session, following decision 6788754429's pattern and the control session's instruction: 20 distinct verification-failure root causes and 20 distinct review-finding root causes. Exhaustion of either: `push -wip and hand off to the next session`. A continuation session starts its own count.

After Hari merges, Hari runs `b10-offline-image.yml` on `main`. No agent dispatches it or the trial.

## 9. Open questions for Hari

None. LA1 to LA5 are accepted by name (6799006446) and LA6 by 6801095762. The warm-up bound and the child deadline are decided (6800669100), and the end-to-end bounds by 6801095762.

## 10. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope. That covers:
- any value, name, member, order rule, header, predicate, bound or literal in sections 3 and 6;
- any file outside 2.1, or any path in 2.2.

In particular, it stops when:
- the offline test needs a workflow change;
- the journal needs a schema version other than 3;
- a verifier change is needed;
- `RunEvidenceReader` rejects the section 3.2 successor entry;
- the image test exceeds 900 s with the section 3.7 delays;
- a child path cannot be bounded as section 3.8 states.

## 11. Plan-check questions

- (a) Effort that scales with a number unknown at planning time: verification failures and review findings. Each is capped at 20 root causes per session, with `push -wip and hand off to the next session`. Adapting existing tests falls under the verification cap. Image runs are fixed at 2, plus 3 mutations.
- (b) Stricter than the parent decisions:
  - the phase reader rechecks `LeaderUp` from the published report bytes (the decision asks only that the child wait);
  - the strict capture-time order of section 3.4;
  - R10's offline positive pair;
  - R10's assertion that no child terminal and no successor ledger row has `error_code` -192;
  - LA2's extension from the leader to its connection, which Hari accepted in 6799006446;
  - the requirement that warm-up `persistence_status` lies in the set the reader already permits;
  - the 20 s leader-report wait, which reuses the existing statistics wait as the bound for the decision's "wait for a report";
  - R10's assertion that the proxy accepted a connection while each role ran.

  Each turns a decided step into a checked one; none changes a design rule. The leader gate itself now follows decision 6797974611's wording: the child waits for a report showing `UP` within the existing wait.
- (c) Dependencies neither on `main` nor in the pull request:
  - Event Hubs behaviour (LA1 to LA5): live-only and accepted for the next trial (6799006446);
  - Docker and image registries for the by-hand image runs, which the test checks itself;
  - Hari's post-merge run of `b10-offline-image.yml` on `main`;
  - the later verifier pull request, which will read the warm-up records and the successor's `capabilities.json`. Section 3.2 pins what it reads.

  The 30 s exit margin's untested parts are LA6, accepted by name (6801095762). librdkafka statements rest on the pinned v2.16.0 documents and source (section 12).

## 12. Sources

- librdkafka v2.16.0, read 2026-11-11:
  - [INTRODUCTION.md, Sparse connections](https://github.com/confluentinc/librdkafka/blob/v2.16.0/INTRODUCTION.md#sparse-connections);
  - [CONFIGURATION.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md): `statistics.interval.ms` "The granularity is 1000ms"; `message.timeout.ms` default 300000; `linger.ms` default 5; `socket.connection.setup.timeout.ms` default 30000; `partitioner` `consistent_random`;
  - [STATISTICS.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md): `brokers.nodeid`, `brokers.state`, `partitions.leader`; partitions keyed by partition id;
  - [src/rdkafka_broker.c](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c): `rd_kafka_broker_needs_connection` and the producer partition serve path.
- Confluent.Kafka v2.16.0: [DeliveryResult.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/DeliveryResult.cs) (`Partition`, `Offset`, `Status`); [Offset.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Offset.cs) (`Value` is `long`); [Partition.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Partition.cs) (`Value` is `int`).
- Code on `main` at `e74849ea`:
  - `src/Parchwell.LoadGen/Run/Abrupt/`: `B10AbruptHost.cs` (lines 57 to 58, 191 to 193, 216 to 218), `B10AbruptParent.cs` (lines 51 and 63), `B10AbruptProducer.cs`, `B10AbruptWire.cs`, `B10AbruptJournal.cs`, `B10AbruptStatistics.cs`;
  - `src/Parchwell.LoadGen/Run/Evidence/RunEvidenceReader.cs` (`ValidateResolvedSetting`);
  - `src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs`;
  - `infra/scripts/phase/b10_abrupt.py`;
  - `tests/Parchwell.LoadGen.Tests/Abrupt/`: `B10AbruptOfflineImageTests.cs`, `B10AbruptEndToEndTests.cs`, `B10AbruptProcessHarness.cs`;
  - `tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs`;
  - `tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs`;
  - `scenarios/B10/scenario.yaml`; `scripts/verify-dotnet.sh`; `.github/workflows/b10-offline-image.yml`; `.github/CODEOWNERS`.
- Decisions and evidence of section 1; plan checks 6798486853 and 6801044256.

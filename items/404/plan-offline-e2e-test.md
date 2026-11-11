# Offline end-to-end test contract-grade plan

Revision 1, issue #404. Short name: `offline end-to-end test`. Baseline: `main` at `aadb0c6d5252d6ca9638d116c1bbf7339c17037e`; `main` has since moved to `0f769fb240d1de50192f4c8eefaf12698002efc7` (capture-loss race fix, PR 425).

This pull request will add one test that runs the redesigned B10 restart path of [decision 6762516530](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762516530) offline: a real producer child against a real local Kafka broker, a real self-SIGKILL, a parent that uploads the child's journal to `FileStorage`, and a real successor that resends what the journal left unconfirmed. That path is not designed in text or written in code yet, so this plan pins what carries over from the earlier design and the prototypes, and leaves every literal that depends on the design pull request to section 13 and the stop rule. It is not implementable until the design pull request merges and its implementation exists.

## 0. Revision history

- Revision 1: first revision. Written by a planning session started by the #347 control session. Begun for the ADR-021 freeze path under [decision 6761981257](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761981257), then retargeted, before any push, to the redesigned path under decision 6762516530 at the control session's request. Not posted, not checked.

## 1. Goal and authority

Goal: one test in `tests/Parchwell.LoadGen.Tests` that runs the redesigned restart path end to end with real processes, a real librdkafka client, a real kill and real timing, and asserts what the whole path produces, so failures that only appear when the stages run together are found before a live trial.

Authority, in creation order:

- Issue body of #404, and the decisions in `items/404/state.md` "Decisions in force" on `main`. None of them governs this test.
- [Decision 6761783572](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761783572) item 5: an offline end-to-end cost study that "decides nothing".
- Decision 6761981257: "Plan option (a0) as its own pull request, in parallel with the partial-frame and storage readiness race fixes. It gets a contract-grade plan, a fresh reproduction check, a plan check and Hari's approval. The implementing tool, the test's run-time budget, and where and how often it runs (every pull request or on demand) go to Hari as open questions. This decision adds no trial gate."
- Decision 6762516530, option 2, "simplify the live restart test". Items 1 to 7 of that decision define the redesigned path; this plan quotes them where it uses them. Its consequences: "The offline end-to-end test plan continues, retargeted to the redesigned path (no stop or resume)." "The partial-frame race fix plan stops". "The PR3b classification plan pauses at Revision 2". The trial gate becomes: "no further B10 trial until the storage readiness race fix merges and the redesign is implemented." Still for Hari: "A design pull request with that text comes next", with open questions "at least: the kill point's policy (position, seed and rate), how a same-instance control pair is formed, the probe rate, the journal's durability on `emptyDir` across SIGKILL (unverified), and the implementing tools."
- The control session's retargeting request (2026-11-10, relaying decision 6762516530) adds one task: measure on a prototype whether bytes a process wrote to a file before SIGKILL are all present afterwards, and cite documentation (section 7). The earlier planning prompt added two items that decision 6761981257 does not state: the real Python acceptance of produced bytes, and what the test does when Docker is absent (Q7 and Q4).
- [Decision 6749565231](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749565231): the plan carries a never-ran inventory (section 6).
- Not authority for this plan: [standing decision 6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614) (four named pull requests; this is not one), and [decision 6761524200](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761524200) point 1 (Codex for PR3b only).

Design text this plan depends on: docs/engineering/dotnet.md "Testing", Component row: "A service against a local Kafka broker in a container | Testcontainers, with Toxiproxy for faults (ADR-016); tests harness logic only, never Event Hubs behaviour". The ADR-021 amendment of the design pull request (not written). This plan changes no design text.

## 2. What carries over, and what the prototypes found

Two prototypes ran in this planning session's Linux workspace (4 CPUs, 15 GB memory, Docker 29.9.2, ext4, cgroup v1 with a cgroup v2 mount that has no memory controller), on 2026-11-10. Every number in this section is a measurement there, not on a GitHub runner, and none is a fact about Event Hubs.

### 2.1 Old-path prototype (ADR-021 freeze path on the baseline)

A test of the old path, built before decision 6762516530: the production `B10AbruptSupervisor` with a real `OwnedProducerProcess` child running the production `B10AbruptProducer` and a real Confluent.Kafka producer through `LocalKafkaFixture`, a real `same_instance` stop and resume, a real `cross_restart` stop and SIGKILL, the real successor in the test process, `FileStorage` and the Python produced-byte checks. Source: scratch file `B10AbruptOfflineEndToEndTests.cs`, SHA-256 `2b3775a3c6bf44ab76ffbbcd1014f3de43db87ddedf66c52d7d85b8ba5ff4960`, not committed.

| Run | Broker delay | Cgroup | Outcome | Wall time |
|---|---|---|---|---|
| p1 | none | substitute | Child `b10_abrupt_guard_failed`: a composition bug in the prototype sent the producer to the Event Hubs host with SASL; fixed in p3 | 66.7 s |
| p3 | none | substitute | `b10_abrupt_exposure_inconclusive`: all five burst originals acknowledged before the stop; first original produce-to-terminal 7 ms | 69.2 s |
| p4 | 30 ms | substitute | Passed through the successor's receipt; the Python check ran 15 tests, `OK`, `b10_abrupt_produced_bytes_checked` | 337.8 s |
| real-cgroup | 30 ms | production | `b10_abrupt_cgroup_unavailable` at the kill checkpoint | 205.2 s |
| rep1 to rep5 | 30 ms | substitute | 5 of 5 passed every assertion, Python check included. `same_instance` selected 4 of 5 in all five; kill checkpoint selected 4 or 5 of 5. Wall time mean 340.2 s, min 339.7 s, max 340.8 s. Supervisor phase 181.4 to 181.6 s, successor 135.2 s, broker start 9.1 to 10.2 s | see left |

Repetitions 6 to 10 were not run: the planning session stopped the batch when decision 6762516530 made the old path obsolete. Repetition 6 was aborted, not failed. No mutation of the old path was run.

What carries over to the new test:

- C1. Transport substitution. The resolved C1 role settings carry the full production baseline, including `bootstrap.servers` and `security.protocol=sasl_ssl` (run p2 recorded them). A local run must apply the role settings first and the substitution last: every `sasl.*` key and `enable.sasl.oauthbearer.unsecure.jwt` removed, `bootstrap.servers` set to the local listener, `security.protocol` set to `plaintext`, every other key unchanged; and the same substitution in any expected-versus-actual settings comparison. p4 recorded the unchanged keys, among them `message.timeout.ms=100`, `enable.idempotence=true`, `max.in.flight=5`, `linger.ms=5`, `acks=-1` and `statistics.interval.ms=15000`.
- C2. A local broker acknowledges in single-digit milliseconds (p3: 7 ms produce-to-terminal; p4 with 30 ms delay: 39 ms). In the redesign "Unconfirmed" means "no terminal record durably journaled at death" (decision 6762516530 item 6). Inference, not measured: with an unshaped local broker and a probe interval longer than about 10 ms, the child would usually die with every probe confirmed, and the successor would resend nothing. Whether the test adds broker latency is Q5.
- C3. Cgroup capture fails in this workspace (real-cgroup run). Whether the redesign keeps a cgroup capture is a design question (Q6).
- C4. The child can be the test assembly itself, started with `-method <child worker test>` as `B10AbruptProcessTests.Command()` starts its child (`tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProcessTests.cs` lines 439 to 445), with a child worker test that returns at once unless its flag variable is `1` (same file, line 459). The prototype's child ran the production producer this way.
- C5. Docker Hub answered `429 Too Many Requests` for the pinned `confluentinc/cp-kafka:7.5.12` digest in this workspace. The same digest was pulled from `mirror.gcr.io` and tagged locally, a workspace step only.
- C6. The `LocalKafkaFixture` change of section 3.1 item 2 worked: the broker advertised the proxy port and every client reached it through the proxy.

### 2.2 Kill probe (journal bytes across SIGKILL)

Section 7.

## 3. Scope

### 3.1 Files allowed to change

1. New test files under `tests/Parchwell.LoadGen.Tests/Abrupt/`: the test class, its child worker class and the helpers of section 4.3. File split and private names are the implementer's choice.
2. `tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs`: one optional parameter on `StartAsync` that sets the advertised PLAINTEXT port (default: the mapped port, as on `main`) and one internal property returning the mapped host port of container port 9092. Only if Q5 is answered A. Every existing caller compiles and behaves unchanged.
3. `items/404/state.md` (factory.md "Item state file") and a new receipt `items/404/offline-e2e-test-receipt.md`.

No protected path, unless Hari answers Q3 with option B (`scripts/verify-dotnet.sh` and a workflow, both protected: `/scripts/` and `/.*/` in `.github/CODEOWNERS`).

### 3.2 Paths and behaviour that must not change

- Every file under `src/`. The test exercises the redesign's production code as merged; it changes none of it.
- Every existing test file other than `LocalKafkaFixture.cs`; no existing assertion changed, loosened, skipped or deleted (docs/engineering/dotnet.md "Testing", rules).
- `infra/**`, `charts/**`, `scenarios/**`, `results/**`, `.github/**`, `scripts/**`, `docs/**`, `AGENTS.md`, `CLAUDE.md`, any `*.csproj`, `Directory.*.props`, `*.sln*`, `packages.lock.json`.
- No new project, assembly reference or NuGet package. The old-path prototype needed only packages `tests/Parchwell.LoadGen.Tests` already uses: Confluent.Kafka, Testcontainers, Microsoft.Extensions.DependencyInjection and xunit (`Integration/BlobPublicationIntegrationTests.cs` lines 13 to 15 and 25; `Component/LocalKafkaFixture.cs` lines 2 to 6).
- No seam around Confluent.Kafka (AGENTS.md "Engineering standards"): the test builds a real `ProducerBuilder<byte[], byte[]>` producer, or uses the production composition if the redesign exposes one without SASL (Q10).

### 3.3 Files shared with other open work

Read on `claude/404-plan` at `3712876` and on `main` at `0f769fb2`, 2026-11-10.

- The redesign's design pull request and its implementation (not planned yet): every production member, file name, record kind and stage this test runs. This is the dependency the stop rule centres on.
- PR3b classification (paused at Revision 2): would change `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs` and `items/404/state.md`. The old-path prototype used `B10AbruptWireProducerTests.TestOptions` and `.FileStorage`; whether the new test does depends on the redesign.
- Storage readiness race fix (continuing): its file set is not known; this test uses `FileStorage`, so it cannot reach that race.
- Partial-frame race fix: stopped by decision 6762516530.
- `items/404/state.md`: every implementation pull request on #404 writes it; whichever merges later rebases it.

## 4. External contracts

### 4.1 Pinned now

- E1. One test class in namespace `Parchwell.LoadGen.Tests.Abrupt`, one `[Fact]` named `Given_LocalKafka_When_RestartPathRunsEndToEnd_Then_UnconfirmedAttemptsAreResentAndPublished`. The class name follows the redesign's production naming and is pinned in the revision after the design pull request (Q11).
- E2. One child worker class with one `[Fact]` that returns at once, and passes, unless environment variable `PARCHWELL_B10_ABRUPT_E2E_CHILD` is `1` (C4). If the redesign's parent starts the published apphost only, the child mechanism is Q12.
- E3. Environment variables the test sets for its child: `PARCHWELL_B10_ABRUPT_E2E_CHILD` = `1` and `PARCHWELL_B10_ABRUPT_E2E_BOOTSTRAP` = the local listener `127.0.0.1:<port>`. None starts with `B10Abrupt__`. The test reads no switch of its own beyond these; prototype switches do not ship.
- E4. Broker: `LocalKafkaFixture` with its pinned image `LocalKafkaFixture.Image`, PLAINTEXT, node ID 1; topic `document-events`, 1 partition, replication factor 1, created by the test. Broker name for any broker inventory: `127.0.0.1:<advertised port>/1`.
- E5. Clock `TimeProvider.System` throughout; storage `FileStorage` in place of blob; the successor in a process distinct from the first child unless Hari answers Q8 with B.

### 4.2 Pinned by the revision after the design pull request (listed so the check can see them)

Probe rate, probe count, kill point (position and seed), journal file name and record kinds, journal hash and upload names, successor resend rule details, new instance and attempt ID formats, header names, any release delay before the successor, the C1 timeout cohort's role in this test (decision 6762516530 item 1 moves it to its own Job; Q13), failure names and stage names, and any Python acceptance entry point for the new bytes. Each is an open question under Q11 until then.

### 4.3 Helpers (behaviour pinned, code shape left to the implementer)

- H1. Composition: the redesign's production child and successor composition with C1's transport substitution only. If production composes through `B10AbruptHost` with OAuth only, the test copies that composition minus authentication, as the old-path prototype did (Q10).
- H2. Delay proxy, only if Q5 is answered A: a TCP listener on `127.0.0.1` port 0, bound before the broker starts so the broker advertises its port; client-to-broker bytes forwarded at once; broker-to-client chunks forwarded in arrival order, each no earlier than its arrival plus the delay of Q5. The old-path prototype's proxy did this.

## 5. Requirements

Each names where its observation is made. Values in angle brackets are pinned by the revision after the design pull request (Q11).

- R1. WHEN the test starts THEN it starts the broker of E4 and every client reaches it only through the local listener (through H2 if Q5 is A).
- R2. WHEN the parent runs the first child THEN the child produces probes at <rate> with instance and attempt ID headers (decision 6762516530 item 2), observed on the topic by reading it back; and appends prepared, accepted and terminal records to its journal file, observed by reading the uploaded journal.
- R3. WHEN the child reaches <kill point> THEN the journal's last durable record before death is the kill intent (item 3), and the parent observes the child's exit by SIGKILL: exit code 137 from `Process.ExitCode`, as the old-path prototype observed.
- R4. WHEN the child has exited THEN the parent hashes the journal and uploads it (item 4), observed as the file in `FileStorage` whose bytes equal the journal file and whose recorded SHA-256 matches; and the parent sent no signal to the child (item 4: "It sends no signals"), observed as the absence of any signal record.
- R5. WHEN the uploaded journal is read THEN at least one attempt has a prepared record and no terminal record (the unconfirmed set of item 6). If the unconfirmed set is empty the test fails, naming the empty set: with no unconfirmed attempt there is nothing for the successor to resend and the path is not tested (C2, Q5).
- R6. WHEN the successor runs THEN it resends exactly the attempts of R5's set, each with a new instance and attempt ID (item 5), observed in the successor's published files and by reading the topic back: for each unconfirmed original, a record with the original's key and payload hash and the successor's new IDs.
- R7. WHEN the topic is read back THEN each record joins to exactly one journaled or resent attempt on instance and attempt ID (item 6); records from confirmed originals, unconfirmed originals that did reach the broker, and resends are counted separately and reported in the test output. Whether the test also runs the verifier's join is Q9.
- R8. WHEN both publications complete THEN the files the redesign publishes are present with the names and count the design pull request sets, and the Python acceptance for the new bytes, if the redesign has one, exits 0 (Q7).
- R9. WHEN any stage fails THEN the test fails with the production exception or failure name, and its output holds the child's and successor's stage lines and the parent's failure record if any. The test retries nothing.
- R10. WHEN the test ends, passed or failed THEN the broker container (and proxy) are disposed, no child process remains, and the test's temporary directory is deleted.
- R11. WHEN `PARCHWELL_B10_ABRUPT_E2E_CHILD` is not `1` THEN E2's method returns and passes without reading any other variable.

Docker absent: `LocalKafkaFixture.StartAsync` throws before R1 completes and the test fails, as every existing `LocalKafkaFixture` test does (Q4).

## 6. Never-ran inventory

Evidence is the trial logs. Trials 1 to 7 are the seven runs of `items/404/plan-successor-fix.md` section 6 (its run table, with zip hashes). Trial 7 is run 42679791413, artifact 13704155185, zip SHA-256 `bee17231882ffeb7ddc3134ba10a00ec4590f20e96dad409c050decf1f22cdcf`; trial 8 is run 42771287992, artifact 13742900343, zip SHA-256 `2c516f2485cf1fa250494040ba3298daf43c6e91b9fb7b2e546a9471dab49e34`; trial 9 is run 42773789291, artifact 13742914191, zip SHA-256 `ff855570ab036fc85c1891b3a555e0f83c89197b34c59bd30dd1b2089fbf9097`. This session downloaded those three zips with `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip` into an empty scratch directory, reproduced the three hashes, and extracted stage lines by script. T7F is trial 7's first-instance log `b10-42679791413-1-c1-r1-loadgen/b10-42679791413-1-c1-r1-loadgen-h9svx.containers.loadgen.log` (SHA-256 `19e738b1d83de514681a62dca4467ad8b5d8ce328cc42e052d4ee3561a5a122f`); T7S is its successor log (SHA-256 `fce88054fccf70591da6f334b7299e446a7ec66cd7b6814fec8740cd17e7209f`); T8F is trial 8's first-instance log `b10-42771287992-1-c1-r1-loadgen/b10-42771287992-1-c1-r1-loadgen-zdwwl.containers.loadgen.log`. Trial 9's artifact holds only `phase-status.json`; decision 6761783572 records that no producer ran.

Every stage of the redesigned path is new code, so none has run live. The rows say which earlier live stage is closest, as evidence of what changes.

| Stage of the redesigned path | Ran live? | This test runs it for real? |
|---|---|---|
| N1. Parent starts the producer child and waits for it | Never. Closest: the supervisor's owned-child start, T7F L80 and T8F L80 `child_started`, which also opened pipes and a handshake the redesign removes | Yes |
| N2. Fixed-rate probes with instance and attempt ID headers | Never. Closest: the burst probes, T7F L78 and T8F L78 `probe_terminal` | Yes, against the local broker |
| N3. Journal appends of prepared, accepted and terminal records to a local file, off the callback path | Never. Closest: the supervisor's journal, written from pipe frames; trial 8's was never published | Yes, on the test machine's disk, not on `emptyDir` |
| N4. Durable kill intent and self-SIGKILL at a pre-fixed point | Never. Closest: the supervisor-sent SIGKILL, T7F L123 `kill_requested`, L124 `exit_observed` | Yes, a real SIGKILL of a real child |
| N5. Parent hashes and uploads the journal after exit | Never | Yes, to `FileStorage`, not blob |
| N6. Workflow check that the first pod has terminated (item 7) | Trial 7, older code (the phase script's first-termination observation; `predecessor-binding.json` is in trial 7's artifact) | No: Kubernetes, out of scope |
| N7. Successor reads the journal and resends every unconfirmed attempt with new IDs | Never. Closest: the successor's single resend, T7S L52 `probe_terminal` | Yes |
| N8. Successor completion and publication | Never: trial 7 failed at completion, T7S L56 `b10_abrupt_guard_failed`; the successor fix merged after | Yes, to `FileStorage` |
| N9. Verifier read-back joined on instance and attempt ID | Never | Only if Q9 is answered A |
| N10. C1 timeout cohort in its own ordinary Job | Never as its own Job; the cohort ran inside the first instance in trials 7 and 8 (T7F L73, T8F L73 `cohort_sealed`) | Q13 |

Out of scope, not run by this test: host composition through `B10AbruptHost` if the test copies it (Q10); the published apphost child; SASL, OAuth and TLS; Azure Blob Storage and the storage readiness race of trial 9; Kubernetes pods, jobs, `emptyDir` and pod termination; the phase orchestration in `infra/scripts/phase/` beyond any produced-byte check; the start-line verifier; process watchdogs; Event Hubs latency, throttling and partitioning; node failure.

## 7. Journal bytes across SIGKILL

The control session asked for this measurement; decision 6762516530 lists "the journal's durability on `emptyDir` across SIGKILL (unverified)" as an open question for the design pull request. This section is evidence for that question, not an answer to it.

### 7.1 Probe

A scratch .NET 10 console program, not committed (`Program.cs` SHA-256 `344e5219d03850649b086e2030aa1dbf4d3cbc96e7c2e246099af2f69357e7d1`, `killprobe.csproj` SHA-256 `c0057009a0dcd164065a2bd6c86149cfd6b1c248bd5c1b88660ed2c84d3d9502`). The parent starts a child and waits for it. The child opens a `FileStream` (4096-byte buffer) on a new file. A background thread appends `bg` records without pause. The main thread appends 2000 `main` records, then one `intent` record, then calls `kill(getpid(), 9)`. Each record is one line of 200 to 499 filler bytes and its own SHA-256, so a torn or altered line is detected. Appends are serialized by one lock, so the background thread can be inside a write when the kill lands. Variants: `flush` calls `FileStream.Flush()` after each record (a `write(2)` to the page cache, no `fsync`); `buffered` never flushes; `fsync` calls `Flush(true)`. The file is on ext4 in `/tmp`.

| Variant | Runs | Exit 137 and all 2000 `main` plus the `intent` record present and intact | Runs with a torn final line | Runs with an invalid complete line | Wall time |
|---|---|---|---|---|---|
| `flush` | 50 | 50 | 0 | 0 | 3.9 s |
| `buffered` | 50 | 50 | 50 | 0 | 3.9 s |
| `fsync` | 20 | 20 | 0 | 0 | 19.4 s |

Reading of the results, each part labelled:

- Measured: in this workspace, every record whose append had completed through `Flush()` before the self-SIGKILL was present and intact afterwards, in 50 of 50 runs, and no run left a partial record even with a second writer active at the kill.
- Measured: without flushing, every run lost the tail held in the user-space buffer, leaving a partial final line. The `main` and `intent` records survived only because the background writer kept pushing the buffer out; the probe does not show that an unflushed `intent` record survives.
- Measured: `fsync` per record cost more wall time here (19.4 s for 20 runs against 3.9 s for 50).
- Not measured: `emptyDir` on an AKS node, any filesystem other than this ext4, container restart, pod deletion, node failure, or memory pressure.

### 7.2 Documentation

- POSIX `write()`, DESCRIPTION (https://pubs.opengroup.org/onlinepubs/9799919799/functions/write.html): after "a write() to a regular file has successfully returned", "Any successful read() from each byte position in the file that was modified by that write shall return the data specified by the write() for that position until such byte positions are again modified." RATIONALE: "If a read() of file data can be proven (by any means) to occur after a write() of the data, it must reflect that write(), even if the calls are made by different threads." The page does not mention process death.
- Linux `write(2)`, NOTES (https://man7.org/linux/man-pages/man2/write.2.html): "A successful return from write() does not make any guarantee that data has been committed to disk." "The only way to be sure is to call fsync(2) after you are done writing all your data." DESCRIPTION: "Note that not all filesystems are POSIX conforming."
- Linux `fsync(2)`, DESCRIPTION (https://man7.org/linux/man-pages/man2/fsync.2.html): it transfers "all modified in-core data of (i.e., modified buffer cache pages for) the file" to the device "so that all changed information can be retrieved even if the system crashes or is rebooted."
- Linux `signal(7)` (https://man7.org/linux/man-pages/man7/signal.7.html), "Standard signals": "The signals SIGKILL and SIGSTOP cannot be caught, blocked, or ignored." "Interruption of system calls and library functions by signal handlers": "I/O operations on disk devices are not interrupted by signals."
- .NET `FileStream.Flush` (https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0): `Flush()` "Clears buffers for this stream and causes any buffered data to be written to the file." `Flush(Boolean)`: "Use this overload when you want to ensure that all buffered data in intermediate file buffers is written to disk." The page does not say that `Flush(true)` calls `fsync`.
- Kubernetes volumes, `emptyDir` (https://kubernetes.io/docs/concepts/storage/volumes/#emptydir): not verified. The fetch returned a model summary for that section, not page text, so no wording from it is cited here.

Confirmed by the cited pages: a read that follows a completed `write()` returns its data; a completed `write()` is not a guarantee against a crash or reboot; `fsync` is. Inferred, not stated by any page read: data a process passed to `write()` before it is SIGKILLed stays readable while the kernel keeps running, because SIGKILL ends the process, not the kernel's page cache. Not verified: the same on `emptyDir` across container restart.

## 8. Invariants and safety rules

- No Azure credentials, no network beyond `127.0.0.1` and the Docker image registry, no workflow dispatch (AGENTS.md hard safety rules 2, 3 and 8).
- No number from sections 2 or 7 enters code or docs as a fact about Event Hubs or `emptyDir` (AGENTS.md hard safety rule 11). Delays and timeouts in the test are test inputs.
- The test asserts outcomes; it never relaxes a production check to pass. A named production failure is a test failure, not a skip.

## 9. Test map

One test. Literal expected values are set by the revision after the design pull request; this map pins which requirement each assertion covers and its boundary.

| Requirement | Assertion | Boundaries |
|---|---|---|
| R1 | Broker started; topic created through the local listener | Docker absent: fails at start (Q4) |
| R2 | Topic records carry both headers; journal holds prepared, accepted, terminal kinds in order per attempt | A record without headers fails |
| R3 | Last journal record before death is the kill intent; exit code 137 | Any other exit code fails; a journal with records after the intent fails |
| R4 | Uploaded bytes equal the journal; hash matches; no signal record | One byte different fails |
| R5 | Unconfirmed set size at least 1 | Size 0 fails with the set named |
| R6 | Resent set equals R5's set; new IDs; key and payload hash equal | One missing or one extra resend fails |
| R7 | Every topic record joins exactly once | An unjoined or doubly joined record fails |
| R8 | Published file set and count; Python exit 0 if applicable | One file missing fails |
| R9, R10, R11 | Output lines; cleanup in `finally`; trivial child pass | Observed in prototype failure runs p1, p3 and real-cgroup |

The predicate map of docs/agents/pull-requests.md does not apply: this is not verifier work.

## 10. Repetitions (flakiness)

Measured so far: the old-path prototype, 5 of 5 passed (section 2.1); its flakiness is not the new test's. For the new test, the implementer runs it 10 times on the merged redesign. Cap: 10 repetitions; exhaustion action: record the gap and continue. Estimate, dated 2026-11-10, basis: the old path's 340.2 s mean wall time, of which the two 120 s release waits and the 40 s timed cohort were about 280 s; the redesign removes the `same_instance` arm and moves the cohort to its own Job, so the new test's time is dominated by any release delay the design keeps: about 30 to 180 s per run. This is an estimate, not a measurement.

## 11. Mutation table

Not run: the production code the mutations would break does not exist yet. The implementer runs each mutation once on the merged redesign, in a scratch worktree, and confirms the test fails; the revision after the design pull request pins each mutation's file and line.

| Mutation (break of the production path) | Requirement that must catch it |
|---|---|
| M1. The child does not kill itself at the kill point (exits normally instead) | R3 |
| M2. The kill intent is not journaled before the kill | R3 |
| M3. The child journals a terminal record before the delivery callback fires | R5, R6 (the attempt leaves the unconfirmed set wrongly) |
| M4. The parent uploads the journal before the child exits | R4 |
| M5. The successor skips one unconfirmed attempt | R6 |
| M6. The successor resends a confirmed attempt | R6 |
| M7. The successor reuses the original attempt ID | R6, R7 |
| M8. The publication omits one file | R8 |

For reference: seven one-line mutations of the old path were prepared and checked to apply exactly once (scratch `mut/apply.py`), but were not run before decision 6762516530.

## 12. Acceptance commands

Run in this planning session, with observed output:

1. Old-path prototype, one run (repeated as rep1 to rep5): `DOTNET_ROOT=/opt/parchwell/dotnet ./Parchwell.LoadGen.Tests -method "Parchwell.LoadGen.Tests.Abrupt.B10AbruptOfflineEndToEndTests.Given_LocalKafka_When_AbruptPathRunsEndToEnd_Then_KillSelectionResendAndExportAreAccepted" -showLiveOutput -noColor`, from `tests/Parchwell.LoadGen.Tests/bin/Release/net10.0` of the scratch worktree, with `PARCHWELL_B10_ABRUPT_E2E_SYNTHETIC_CGROUP=1`. Observed: `Total: 1, Errors: 0, Failed: 0`, exit 0, 339.7 to 340.8 s.
2. Python produced-byte check on the p4 export: `python3 check_b10_abrupt_producers.py <export>` from `infra/scripts/phase/tests`. Observed: `Ran 15 tests`, `OK`, `b10_abrupt_produced_bytes_checked`, exit 0.
3. Kill probe: `./out/killprobe parent flush 50 2000`, `parent buffered 50 2000`, `parent fsync 20 2000`. Observed: the three result lines of section 7.1.

For the pull request (pinned commands; the test name is completed in the revision after the design pull request):

4. `./scripts/verify-dotnet.sh`: exit 0, if Q3 is answered A; the new test passes inside it.
5. The new test alone, 10 times, with the command shape of command 1 and no prototype switch: 10 passes, or each failure recorded with its failure name (section 10).
6. Each mutation of section 11, once: the test fails, with the requirement named.

## 13. Open questions for Hari

Each has options, a recommendation and the strongest counterargument. This plan decides none of them.

- Q1. Implementing tool. A, Claude Code; B, Codex. Recommendation: A: the implementer must run a Docker-backed test many times, and this Claude Code cloud environment ran Docker, while a Codex environment's Docker support is not verified; and Codex then reviews a pull request it did not write (ADR-018). Counterargument: the plan comes from Claude Code, so a Codex implementation would test whether the plan stands on its own.
- Q2. Run-time budget. A, accept the production timing of the redesign with a hard test bound of 2.5 times the slowest of the 10 repetitions, failing on expiry; B, no bound beyond the job's; C, test-only shorter timings. Recommendation: A. The factor 2.5 is a proposal, not a measurement. Counterargument for C: a release delay of the order of the old 120 s would dominate every pull request's run; C removes it but stops testing the production timing.
- Q3. Where and how often it runs. A, in every pull request, inside `tests/Parchwell.LoadGen.Tests`, which `scripts/verify-dotnet.sh` already runs in the required `.NET verification` job (its "Verify .NET foundation" step took 9 min 7 s in run [42771084767](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42771084767) on the baseline, by the jobs API's step timestamps); B, a trait filter excluded by `scripts/verify-dotnet.sh`, plus a new `workflow_dispatch` workflow (protected paths); C, on demand: the test returns and passes unless an environment variable is set, as `Given_WireOutputDirectory_When_Run_Then_WritesProducedArtifacts` does (`B10AbruptWireProducerTests.cs` lines 31 to 38). Fact: `scripts/dotnet-verification.proj` lines 42 and 56 reject a report with any skipped test, so `Assert.Skip` serves neither B nor C. Recommendation: A. Counterargument: a timing-dependent test on shared runners can flake and block unrelated work; CPU contention with parallel test classes was not measured on a runner.
- Q4. Docker absent. A, the test fails, like every existing `LocalKafkaFixture` test; B, it returns and passes with an output line; C, `Assert.Skip`. Recommendation: A. Counterargument: a developer without Docker cannot get a green local run; B hides the gap, and C fails the report check anyway.
- Q5. Broker latency. Facts: C2. A, the H2 delay proxy with a fixed delay set after the redesign's probe rate is known; B, Toxiproxy (ADR-016's tool) as a second container through the existing Testcontainers package and its HTTP API: a pinned image, no NuGet package; C, no added latency. Recommendation: A, unless the design's probe rate and kill point guarantee an unconfirmed attempt without it, which C2 suggests they will not locally. Counterargument: any delay is an artificial shape that decides the unconfirmed set; it does not model Event Hubs, and it is not the Toxiproxy setup ADR-016 names.
- Q6. Cgroup capture. If the redesign keeps one: A, a test-side substitute; B, the production capture, which fails in this workspace (C3) and is not verified on a runner. Recommendation: decide after the design pull request. Counterargument for A: it removes the one kernel-facing stage near the kill.
- Q7. Python acceptance. Not in decision 6761981257; added by the planning prompt. A, the test runs the phase scripts' produced-byte check for the new bytes, if the redesign has one, which adds `python3` to the `.NET verification` job's needs; B, the check is a pull request acceptance command only; C, drop it. Recommendation: A. Counterargument: it couples the .NET check to a Python interpreter whose presence on that job is not verified (the `lifecycle-verification` job asserts 3.12, `.github/workflows/dotnet-verification.yml` line 52; the .NET job does not).
- Q8. Successor process. A, a second child process of the test; B, in the test process. Recommendation: A, since decision 6762516530 item 5 says "a fresh pod" and the successor must not share the dead child's state; the old-path prototype used B. Counterargument: B is smaller, and neither A nor B is a fresh pod.
- Q9. Verifier join. A, the test runs the verifier's read-back join (decision 6762516530 item 6) through the verifier code, once that exists; B, the test does its own read-back join (R7). Recommendation: B for this pull request, A as a follow-up once the verifier work resumes. Counterargument: B re-implements the join, so a verifier defect would pass.
- Q10. Composition. If production composes the child and successor only through `B10AbruptHost` with OAuth: A, the test copies that composition minus authentication (the old-path prototype did); B, the design pull request exposes a composition that accepts the transport substitution, which is a `src/` change outside this plan. Recommendation: B, raised in the design pull request. Counterargument: B shapes production code for a test; A cannot catch host composition failures such as trials 1 to 3.
- Q11. Every literal of section 4.2, R2 to R8's angle-bracket values, the test class name of E1 and the mutation sites of section 11: set by the design pull request; the plan gets a revision and a fresh check before implementation.
- Q12. Child launch. If the redesign's parent starts only the published apphost: A, the test runs the published apphost (as `B10AbruptPublishedApphostTests` reads `PARCHWELL_B10_ABRUPT_PUBLISHED_APPHOST`, `B10AbruptProcessTests.cs` line 479); B, the parent accepts a child command, as `OwnedProducerProcess.Start` does today. Recommendation: decide in the design pull request.
- Q13. C1 timeout cohort. Decision 6762516530 item 1 moves it to its own ordinary Job. A, out of this test; B, run before the restart pair in the same test. Recommendation: A: the cohort becomes an ordinary producer run, and it cost 40 s per old-path run. Counterargument: the live order runs both, and an interaction between them would go untested.
- Q14. Journal durability. Section 7 measured `Flush()` after each record as enough across a self-SIGKILL on this ext4, and `fsync` per record as slower. Whether the redesign's journal uses `Flush()`, `fsync` or something else, and whether `emptyDir` behaves the same, is the design pull request's question (decision 6762516530 "Still for Hari"); this test can only cover the test machine's disk.
- Q15. Environment assumptions, accept each by name or reject: DA1, Docker is available and can pull the pinned image (C5 shows Docker Hub can refuse with 429); DA2, `python3` 3.10 or later is on the .NET job's `PATH` if Q7 is A; DA3, the test machine allows a child to signal itself and the parent to read its exit status, as `B10AbruptProcessTests` already requires.

## 14. Stop rule

The implementer does not start until the design pull request of decision 6762516530 has merged, its implementation has merged, and this plan has a revision that pins section 4.2 and Q11's values against that merged code and has passed a fresh check and Hari's approval. After that, the implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope: any name or input in section 4, any requirement in section 5, any file outside 3.1 or any file in 3.2. It also stops when a change merged by another pull request (3.3) changes a member, format, file set, stage name, failure name or behaviour this test reads or asserts, and the test no longer passes unchanged on the new `main`. A test failure that a later merge introduced is a finding to report, not a reason to change an assertion.

## 15. Plan-check questions

- (a) Effort that scales with a number unknown at planning time: the 10 repetitions (cap 10, record the gap and continue) and the 8 mutation runs (fixed count). The planning session's own prototype runs were capped at 10 repetitions; 5 ran (section 2.1).
- (b) Where the plan is stricter than its parents: R5 fails on an empty unconfirmed set, which decision 6762516530 does not require; R7 adds a read-back join; Q7 adds the Python acceptance, which decision 6761981257 does not ask for.
- (c) Tests that depend on code, data or behaviour neither on `main` nor in the pull request: the whole redesigned path (not written; stop rule), the pinned Docker image (DA1), `python3` (DA2), and the kernel's page-cache behaviour across SIGKILL (section 7: measured here, inferred elsewhere).

## 16. Deployment assumptions

None about the deployed container, image, chart or runtime: this test runs only on a developer or CI machine. The `emptyDir` durability question belongs to the design pull request (Q14). Environment assumptions DA1 to DA3 are under Q15 until Hari accepts each by name.

## 17. Sources

- Decisions [6761783572](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761783572), [6761981257](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761981257), [6762516530](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762516530), [6749565231](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749565231), [6740526438](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6740526438) (exposure inconclusive kept for a fully acknowledged burst).
- Code at the baseline: `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs`, `B10AbruptSupervisor.cs`, `B10AbruptProducer.cs`, `B10AbruptPublisher.cs`, `B10AbruptCgroup.cs` (`Resolve` requires a `0::` line and one `cgroup2` mount; `Capture` reads `memory.events.local`), `OwnedProducerProcess.cs`; `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProcessTests.cs`, `B10AbruptWireProducerTests.cs`; `tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs`; `infra/scripts/phase/tests/check_b10_abrupt_producers.py`; `scripts/verify-dotnet.sh`; `scripts/dotnet-verification.proj`; `.github/workflows/dotnet-verification.yml`; `.github/CODEOWNERS`; `docs/engineering/dotnet.md` "Testing".
- Trial artifacts and logs of section 6, and `items/404/plan-successor-fix.md` section 6 on `claude/404-plan`.
- Pages of section 7.2, read 2026-11-10.

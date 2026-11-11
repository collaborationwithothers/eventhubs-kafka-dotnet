# Offline end-to-end test contract-grade plan

Revision 4, issue #404. Short name: `offline end-to-end test`. Baseline: `main` at `3a4ee0b140a209c2cdeae0002a77c5349a6aabc7`, the merge of execution part 1 (pull request 430), read 2026-11-11.

This pull request adds one test that builds the LoadGen container image and runs its real parent, self-killing child and successor inside containers against a local Kafka broker, and one workflow, dispatched by hand on `main`, that runs it. The test proves three deployment assumptions of the B10 restart, inside the built image: the journal survives the child's self-SIGKILL (DA1), the parent sees the death through `WaitForExit` (DA5), and the exit code is 137 (DA6). It also measures the time from the cut original's acceptance to the kill, which nobody has measured yet.

## 0. Revision history

- Revision 1 (`f75d6ac3`): written before the redesign existed; held, never posted or checked.
- Revision 2 (`f632352`): rewritten against the merged part 1 code under decision 6787880185. Everything in Revision 1 is replaced. Its prototype and kill-probe measurements (Revision 1 sections 2 and 7) are not carried forward as evidence; they ran on superseded code or a scratch program. Checked in [6788170748](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788170748): three blocking findings.
- Revision 3: answers that check under decision [6788215099](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788215099). B1: R7 no longer requires `publication_complete` exactly once (the publisher and the parent each mark it). B2: section 4.2 adds the remaining restart-pod settings (group 1654 on mounts, non-root, default seccomp profile, CPU and memory limits); the container-runtime residual is DA-iii in section 7, accepted by name. B3: R10 bounds the whole test method, not only the container span. N1: the separate reproduction check is waived (section 1). N2: the Lifecycle verification job is not called required. N3: section 4.4 states what the producer-factory override also bypasses. N4: section 2.1 item 2 names the one change to existing modes. N5: section 4.5 requires its own one-test count check.
- Revision 3 (`0c5dd5f`) was checked in [6788423093](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788423093): one blocking finding (resource requests not reproduced), answered by decision [6788464379](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788464379).
- Revision 4: records decision [6788464379](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788464379) and the check's N4 and N5; no requirement, contract or test changes. Section 4.2 drops the unsourced sentence about requests and section 7 records their omission as accepted (point 1). Section 7 adds the runner kernel to DA-iii (point 3). Section 1 records that the test build is outside the 900 s bound (point 2), which R10 already states. Section 8 states what a surviving mutation means (N4). Section 2.4 says part 2 may change the chart, not that it does (N5).

## 1. Goal and authority

Goal: before the one-repeat B10 trial, show on a GitHub runner, from the built image, with real processes, a real librdkafka client, a real self-SIGKILL and real timing, that DA1, DA5 and DA6 hold, and record the accepted-to-kill interval.

Authority, in creation order (issue #404 comments):

- [6761981257](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761981257): the offline test is its own pull request; implementing tool, run-time budget, and where and how often it runs go to Hari.
- [6762516530](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762516530): the redesign; "The offline end-to-end test plan continues, retargeted to the redesigned path (no stop or resume)."
- [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) Q15: "the parent requires the child's exit code to equal the value the execution pull request's offline test records for a self-SIGKILL on the pinned runtime; any other value fails intended termination."
- [6769742939](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769742939): the offline test is required before the one-repeat trial for DA1 ("the flushed `emptyDir` journal is intact when the parent reads it after the child's self-SIGKILL") and DA5 ("the parent observes death through `Process.WaitForExit` with no pipe poll"), and "also measures the time from the cut original's `accepted` record to the self-SIGKILL, because DA2 depends on it." DA2 to DA4 are accepted by name for the trial only.
- [6774981258](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6774981258) Q6: "DA6 (the deployed runtime gives exit code 137 for the self-kill) is settled by the offline end-to-end test running the child inside the built image".
- [6787880185](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787880185): Q1 option A (startup hook replaces exactly storage and Kafka transport; no `src/` change), Q2 option B (`workflow_dispatch`-only workflow restricted to `main`; Hari runs it before each B10 trial; its log is the gate evidence; the test returns and passes unless its variable is set), Q3 option A (two residual assumptions accepted by name, section 7), and the defaults: Codex implements; 900 s run bound excluding the image build; 60 minute job timeout; real production timing; exit code other than 137 fails and the implementer stops; Docker absent while the variable is set fails; no new project or package; the DA2 interval as defined in section 4.6.

- [6788215099](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788215099): point 1 accepts by name a third deployment assumption, "the container runtime and its default seccomp profile differ (Docker with its default profile on the runner; containerd with `RuntimeDefault` on AKS)", with every other restart-pod setting reproduced in the test; point 2 waives the separate fresh reproduction check of 6761981257: "the plan check of each revision stands in place of" it.
- [6788464379](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788464379): point 1 accepts by name that the test does not reproduce the restart Job's resource requests (CPU 1, memory 2Gi), only its limits; point 2 confirms that the 900 s bound excludes both the image build and the test build; point 3 adds to DA-iii that the runner's Linux kernel is not the AKS node's kernel.

Design text: docs/blueprint.md ADR-021 protocol rules 2, 3, 6, 7 and 10 as merged; docs/engineering/dotnet.md "Testing" (Testcontainers against a local broker, harness logic only, never Event Hubs behaviour). This plan changes no design text and no rule.

## 2. Scope

### 2.1 Files allowed to change

1. New `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptOfflineImageTests.cs`: the test of section 4.1. Helpers may live in this file or in further new files under the same folder.
2. `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProcessHarness.cs`: the one new control mode and hook record of section 4.4. Existing control modes are unchanged except for the `hook-loaded.jsonl` append that section 4.4 requires in every mode.
3. New `.github/workflows/b10-offline-image.yml` (protected path `/.*/` in `.github/CODEOWNERS`; Hari merges).
4. `infra/scripts/lifecycle/check-offline.sh`: one entry in `check_workflows`' `expected_triggers` (section 4.5). It runs in the `Lifecycle verification` job (`.github/workflows/dotnet-verification.yml` line 84). That job is not a required check: the branch rules for `main` list only `.NET verification`.
5. `items/404/state.md` (the implementing session writes it, docs/agents/factory.md "Item state file") and new `items/404/offline-e2e-test-receipt.md`.

### 2.2 Must not change

- Every file under `src/` (decision 6787880185 point 1). In particular `B10AbruptParent.cs` line 25 (`exitCode == 137`) stays.
- Every other test file, including `B10AbruptEndToEndTests.cs` and `PublishedApphostStartupHook.cs`; no existing assertion loosened, skipped or deleted.
- Every other workflow, `docs/**` (including `docs/operations/workflows/`), `scripts/**`, `charts/**`, `infra/` other than item 4, `scenarios/**`, `results/**`, any `*.csproj`, `Directory.*.props`, `*.sln*`, lock files, `AGENTS.md`, `CLAUDE.md`. No new project, assembly reference or package.

### 2.3 Required CI that the change reaches

- `scripts/verify-dotnet.sh` runs `tests/Parchwell.LoadGen.Tests` in the required `.NET verification` job; there the new test's variable is unset, so it returns and passes (R1). `scripts/dotnet-verification.proj` `ValidateReport` rejects any skipped test (lines 42 and 56), so the test must not use `Assert.Skip`.
- `infra/scripts/lifecycle/check-offline.sh` (item 4) runs in the `Lifecycle verification` job and `infra/scripts/tests/test_tools_offline.py` line 28 runs `check_workflows` against the committed tree.
- No existing workflow, trigger or required check changes.

### 2.4 Work shared with other open plans

Execution part 2 (decision 6787849492) may change the chart, phase scripts and operations documents, not these files. If part 2 changes the restart Job's pod settings that section 4.2 copies (user, group, non-root, `fsGroup`, read-only root, capabilities, privilege escalation, seccomp profile, limits, `/work`, `/tmp`, command and arguments), the stop rule applies.

## 3. Requirements

Each names where it is observed. "The test" is the method of section 4.1.

- R1. WHEN `PARCHWELL_B10_OFFLINE_IMAGE` is unset or empty THEN the test returns and passes without reading any other variable, starting any container or touching Docker.
- R2. WHEN it is set and `PARCHWELL_B10_OFFLINE_SOURCE_COMMIT` is not 40 lowercase hex, or `PARCHWELL_B10_OFFLINE_EVIDENCE` is empty or names an existing file, or the Docker daemon is unreachable, or the image is absent THEN the test fails, naming which.
- R3. WHEN the test starts THEN it starts `LocalKafkaFixture` (pinned image `LocalKafkaFixture.Image`), creates topic `document-events` with 1 partition and replication factor 1, reads broker names from metadata as `host:port/id`, and writes the start-line binding of section 4.3.
- R4. WHEN the parent container (section 4.2) runs THEN it exits 0, and the hook records (section 4.4) show exactly one `parent` record with `pid` 1, `uid` 1654, `process_path` `/app/Parchwell.LoadGen` and `loadgen_assembly` `/app/Parchwell.LoadGen.dll`, and at least one `child` record with `uid` 1654, `pid` not 1 and the same two paths. This proves the image's own binaries ran under the hook.
- R5 (DA6). WHEN the parent has published THEN the first-publication `termination.json` (envelope kind `termination`) has, in its `data`, `termination` `intended_self_kill`, `exit_observed` true, `exit_code_local_observation` 137, `successor_permitted` true, `capture_gaps` empty, `cgroup_before` and `cgroup_after` non-null and equal, and a nonempty `kill_unconfirmed_attempt_ids`. An exit code other than 137 fails with `b10_offline_image_exit_code:<value>` (stop rule).
- R6 (DA1). WHEN the parent has published THEN the published `journal.jsonl` bytes equal the bytes of `/work/loadgen/abrupt/journal.jsonl` left in the parent container's `/work` directory, their SHA-256 and length equal the `journal` descriptor in the published `abrupt-manifest.json`, every line ends with a line feed and parses, `B10AbruptJournal.Read` with the first identity returns no gaps and no failures, and the last producing record (`B10AbruptWire.IsProducingKind`) is the single `kill_intent`.
- R7 (DA5). WHEN the parent container's standard error is read THEN its `b10_abrupt_stage` lines with `role` `parent` hold `exit_observed` exactly once, and the first occurrences of `parent_registered`, `child_started`, `exit_observed`, `journal_read` and `publication_complete` appear in that order. `publication_complete` is printed twice on correct code (`B10AbruptPublisher.cs` line 91 inside `PublishFirstAsync`, then `B10AbruptParent.cs` line 115); the test must not require it once.
- R8 (DA2 interval). WHEN the journal is read THEN the test computes, with the `kill_intent` record's `clock_frequency`: `cut_accepted_to_kill_intent_microseconds` = (`kill_intent` `monotonic_ticks` minus the `monotonic_ticks` of the `accepted` record whose `attempt_id` equals `kill_intent.data.cut_attempt_id`) times 1000000 divided by the frequency; and `kill_intent_to_exit_observed_microseconds` = (the parent `exit_observed` stage line's `ticks` minus the `kill_intent` ticks), same conversion. Either value negative, or no such `accepted` record, fails the test. No threshold applies; both are recorded only.
- R9. WHEN the successor container (fresh `/work`, same image and storage, section 4.2) runs with the predecessor binding of section 4.3 THEN it exits 0; its receipt `successor/completion.json` (envelope kind `successor_receipt`) lists invocations whose `predecessor_attempt_id` values equal `kill_unconfirmed_attempt_ids` in order; and reading `document-events` back finds, for each invocation, a record whose `attempt-id` header is its `attempt_id`, whose key, `sequence` header and payload SHA-256 equal the predecessor's `prepared` record, and whose `instance-id` header is `successor`. These are the assertions of `B10AbruptEndToEndTests` lines 82 to 110, applied to container output.
- R10. WHEN more than 900 s pass from the test method's start (with the variable set) to the end of its cleanup THEN the test fails with `b10_offline_image_bound_exceeded`. The bound covers broker start, topic creation, both containers, read-back and cleanup. The image build and test build are separate workflow steps, outside it.
- R11. WHEN the test passes THEN it writes the observation line of section 4.6 to the file `PARCHWELL_B10_OFFLINE_EVIDENCE` names, creating it new.
- R12. WHEN the test ends, passed or failed THEN no container it started remains, the broker is disposed, and on a pass its temporary directories are deleted. Files the containers created as uid 1654 may be removed through a container of the same image run as uid 1654; the mechanism is the implementer's.
- R13. WHEN any container exits nonzero or a bound expires THEN the failure message carries that container's last 16384 characters of output.

## 4. External contracts

### 4.1 Test

Class `Parchwell.LoadGen.Tests.Abrupt.B10AbruptOfflineImageTests`, one `[Fact]` `Given_BuiltImage_When_ParentChildAndSuccessorRunInContainers_Then_JournalExitAndResendsAreObserved`. Variables it reads: `PARCHWELL_B10_OFFLINE_IMAGE` (a local image reference, for example `parchwell/loadgen:offline-<commit>`), `PARCHWELL_B10_OFFLINE_SOURCE_COMMIT`, `PARCHWELL_B10_OFFLINE_EVIDENCE`. No other switch ships.

### 4.2 Containers

Both containers use the image of `PARCHWELL_B10_OFFLINE_IMAGE`, and the restart Job's pod settings in `charts/parchwell/templates/jobs.yaml` lines 66 to 92 and 218 to 240: command `/app/Parchwell.LoadGen`, argument `--b10-abrupt-parent` (parent) or `--b10-abrupt-successor` (successor); user and group 1654, so the process is non-root (`runAsUser`, `runAsGroup`, `runAsNonRoot`); the mounted runner directories owned by uid and gid 1654 (the counterpart of `fsGroup: 1654`); read-only root filesystem; all capabilities dropped; no new privileges (`allowPrivilegeEscalation: false`); Docker's default seccomp profile, with no `seccomp` override (the counterpart of `seccompProfile: RuntimeDefault`, not claimed identical, DA-iii); CPU limit 3 and memory limit 12 GiB (`limits`; the `requests`, CPU 1 and memory 2Gi, are not reproduced, accepted by name in section 7); no init process and no shared PID namespace, so the entrypoint is PID 1; private cgroup namespace; host networking, so the client reaches `127.0.0.1:<mapped port>`. Mounts: `/work` and `/tmp` are directories on the runner's disk, writable by uid 1654, fresh for each container; `/bindings/b10-abrupt` read-only, holding `start-line.json` (parent) or `predecessor.json` (successor); `/storage` read-write, the same runner directory for both; `/hook` read-only, the test build's output directory.

Environment, both containers: `B10Abrupt__Enabled=true`, `Purpose=diagnostic_trial`, `RunId=offline-image`, `ScenarioId=B10`, `ConfigurationId=C1`, `Phase=C1`, `Repeat=1`, `SourceCommit=<PARCHWELL_B10_OFFLINE_SOURCE_COMMIT>`, `ImageReference=parchwell/loadgen@sha256:<the image ID's 64 hex>` (a synthetic reference from the local image ID; not a registry digest), `NamespaceName=parchwell`, `BlobEndpoint=https://example.blob.core.windows.net`, `ScenarioSourceRoot=/app`, `OutputRoot=/work/loadgen` (each with the `B10Abrupt__` prefix); `Telemetry__ConnectionString`, `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` and `AZURE_FEDERATED_TOKEN_FILE` with the synthetic values `B10AbruptProcessFixture.Command` uses (`B10AbruptRealProcessTests.cs` lines 151 to 157), the token file path under `/work`; `DOTNET_STARTUP_HOOKS=/hook/Parchwell.LoadGen.Tests.dll`. Parent only: `B10Abrupt__Role=parent`, `JobName=offline-image-c1-r1-loadgen-restart`, `PodName=first`, `PodUid=first-uid`, `StartLineBindingPath=/bindings/b10-abrupt/start-line.json`. Successor only: `B10Abrupt__Role=successor`, `JobName=offline-image-c1-r1-loadgen-successor`, `PodName=successor`, `PodUid=successor-uid`, `PredecessorBindingPath=/bindings/b10-abrupt/predecessor.json`. The test writes `/work/loadgen/test-control.json` into each container's `/work` before start (section 4.4).

### 4.3 Bindings

`start-line.json`: the shape `B10AbruptEndToEndTests.WriteBrokerBinding` writes (lines 116 to 125), with `run_id` `offline-image`, the metadata broker names, and `source_commit` equal to `PARCHWELL_B10_OFFLINE_SOURCE_COMMIT`. `predecessor.json`: the shape of `B10AbruptEndToEndTests` lines 71 to 76, with `job_name` `offline-image-c1-r1-loadgen-restart`, `pod_name` `first`, `pod_uid` `first-uid`, the parent's `image_reference` and `source_commit`, the published termination descriptor, the start-line binding, and `release_utc` from the `kill_intent` record.

### 4.4 Hook mode

`B10AbruptProcessHarness.Control` gets mode `image-kafka`, used with `RealTime` true, `StorageRoot` `/storage` and `BootstrapServers` `127.0.0.1:<mapped port>`. In this mode `Install` replaces exactly the storage (`FileStorage(StorageRoot)` for `IB10AbruptStorage`) and the producer factory (the existing `BootstrapServers` branch: copy of the resolved configuration with `bootstrap.servers` set to the local listener, `security.protocol` PLAINTEXT, `sasl.mechanism` and `sasl.oauthbearer.config` removed, every other key unchanged). It installs no cgroup override and no clock, so production `B10AbruptCgroup` capture and `TimeProvider.System` run. The producer-factory override (`B10AbruptHost.cs` lines 212 to 213) also bypasses the production `CreateProducerBuilder` OAuth session and the `SetLogHandler` librdkafka log capture, so neither runs in this test; neither bears on DA1, DA5 or DA6. The receipt states this beside the gate evidence. In every mode, `Install` first appends one JSON line to `<directory>/hook-loaded.jsonl`: `{"role":<B10Abrupt__Role>,"pid":<Environment.ProcessId>,"uid":<the real uid from /proc/self/status>,"process_path":<Environment.ProcessPath>,"loadgen_assembly":<typeof(B10AbruptHost).Assembly.Location>}`. The existing `child-pids.txt` append stays. Basis for loading the image's own `Parchwell.LoadGen.dll`: a startup hook "must not depend on any assemblies outside of the app's TPA list" and "There is no extra resolution logic for startup hooks" ([host startup hook design, v10.0.0](https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/docs/design/features/host-startup-hook.md), read 2026-11-11); R4 checks the result rather than relying on it.

### 4.5 Workflow and checker entry

`.github/workflows/b10-offline-image.yml`:

```yaml
name: B10 offline image test

on:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  offline-image:
    name: B10 offline image test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-24.04
    timeout-minutes: 60
    steps:
      - name: Check out repository
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          persist-credentials: false
      - name: Set up pinned .NET SDK
        uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json
      - name: Build LoadGen image
        run: <build step>
      - name: Build tests
        run: <test build step>
      - name: Run offline image test
        run: <run step>
      - name: Print observation
        run: <print step>
```

Step contents, pinned by behaviour (exact shell is the implementer's):

- Build LoadGen image: the trial's publish command (`infra/scripts/phase/phase.py` lines 558 to 562), `dotnet publish src/Parchwell.LoadGen/Parchwell.LoadGen.csproj --configuration Release -p:RestoreLockedMode=true -p:NuGetLockFilePath=packages.linux-x64.lock.json -r linux-x64 --self-contained false /t:PublishContainer -p:ContainerImageTag=offline-${{ github.sha }}`, with `-p:ContainerArchiveOutputPath=<runner temp archive>` in place of `-p:ContainerRegistry=...`; then `docker load` of the archive. Prints the loaded image ID.
- Build tests: Release build of `tests/Parchwell.LoadGen.Tests` with locked restore.
- Run: only the method of section 4.1, with `PARCHWELL_B10_OFFLINE_IMAGE=parchwell/loadgen:offline-${{ github.sha }}`, `PARCHWELL_B10_OFFLINE_SOURCE_COMMIT=${{ github.sha }}` and `PARCHWELL_B10_OFFLINE_EVIDENCE` under the runner temp directory; the step fails unless its xUnit XML report, checked by `dotnet msbuild scripts/dotnet-verification.proj -t:ValidateReport -p:ExpectedAssembly=Parchwell.LoadGen.Tests`, passes, and a separate check in the same step, reading that report, finds exactly one test case, passed. `ValidateReport` checks zero failures, skips and not-run tests, not the count.
- Print observation: `cat` of the evidence file, so the line of section 4.6 is in the job log.

No other trigger, no `id-token` permission, no secret, no `azure/login`, no artifact upload, no concurrency block.

`check-offline.sh` `expected_triggers` gains `'b10-offline-image.yml': {'workflow_dispatch': ''},`.

### 4.6 Observation line

One line, keys in this order, single spaces, no other text:

```
b10_offline_image source_commit=<40 hex> image_id=sha256:<64 hex> runtime=<termination runtime_version> kernel=<runner /proc/sys/kernel/osrelease> parent_exit=0 exit_code_local_observation=137 termination=intended_self_kill journal_records=<count> kill_unconfirmed=<count> cut_accepted_to_kill_intent_microseconds=<decimal> kill_intent_to_exit_observed_microseconds=<decimal> successor_exit=0 consumed_resends=<count>
```

Decimals use invariant culture. Every value is a local observation on that runner, not a fact about Event Hubs or AKS. The cut is the cross-restart arm's (`kill_intent.data.cut_attempt_id`), not the control arm's `cut` record: `B10AbruptEndToEndTests` lines 57 to 61 measure from the control cut and include the 120 s release, a different interval (decision 6787880185 point 4).

## 5. Invariants and safety rules

- No Azure credential, no Azure endpoint reached, no dispatch by an agent; the workflow runs only on `main` (AGENTS.md hard safety rules 3, 8 and 9). It has no pull request trigger.
- The test asserts outcomes; it never relaxes a production check. A production failure name is a test failure.
- No number from this test enters code or documentation as a fact about Event Hubs, AKS or `emptyDir` (hard safety rule 11). Bounds are policy (decision 6787880185 point 4).
- Real production timing: both 120 s releases run; no test clock in this mode.

## 6. Test map

| Requirement | Test and assertion | Literal values and boundaries |
|---|---|---|
| R1 | Variable unset in `scripts/verify-dotnet.sh`: test passes in the LoadGen report; no Docker call | Empty string behaves as unset |
| R2 | Each listed precondition | Commit of 39 hex or uppercase fails; existing evidence file fails |
| R3 | Topic and binding written | 1 partition, replication factor 1 |
| R4 | `hook-loaded.jsonl` records | parent `pid` 1, `uid` 1654, `/app/Parchwell.LoadGen`, `/app/Parchwell.LoadGen.dll`; a child record with `pid` other than 1 |
| R5 | `termination.json` fields | `intended_self_kill`, `true`, `137`, `true`, `[]`, equal non-null cgroup, nonempty kill set; 136 or 138 fails with `b10_offline_image_exit_code:<n>` |
| R6 | Byte and hash equality, `Read` result, last producing kind | One byte different fails; a producing record after `kill_intent` fails |
| R7 | Stage line order | `exit_observed` exactly once; first occurrences of the five stages in order; `publication_complete` twice passes |
| R8 | Two intervals | Negative fails; missing `accepted` fails |
| R9 | Receipt order and read-back | One missing or extra resend fails |
| R10 | Bound | 900 s, from test method start to end of cleanup |
| R11, R13 | Evidence file, failure output | File created new; 16384-character tail |
| R12 | Cleanup in `finally` | No container left |

Mutations, each applied once in a scratch worktree, image rebuilt, test run once, expected to fail with the named requirement (fixed count 3; no cap needed):

| Mutation | Expected failing requirement |
|---|---|
| M1. `B10AbruptParent.cs` line 25: `exitCode == 137` to `exitCode == 138` | R5 (`termination` `unexpected_exit`) |
| M2. `B10AbruptJournal.cs` line 80: delete `file.Flush();` | R5 or R6 (journal tail lost at the kill) |
| M3. `B10AbruptParent.cs`: replace the `WaitForExit` wait bound with 0 ms | R5 (`exit_observed` false) or R7 |

The pull-request predicate map of docs/agents/pull-requests.md does not apply: this is not verifier work.

## 7. Deployment assumptions

- DA-i, accepted by name ([6787880185](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787880185) point 3 (i)): the trial's image is built by the same publish command at the trial's commit, not the byte-identical image this test ran.
- DA-ii, accepted by name (same decision, point 3 (ii)): on AKS `/work` is an `emptyDir` on the node disk under containerd; here it is a runner directory mounted into a Docker container.
- DA-iii, accepted by name ([6788215099](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788215099) point 1): the container runtime and its default seccomp profile differ (Docker with its default profile on the runner; containerd with `RuntimeDefault` on AKS). Every other restart-pod setting is reproduced (section 4.2), except the resource requests below. Added by [6788464379](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788464379) point 3: the runner's Linux kernel is not the AKS node's kernel.
- Resource requests, accepted by name ([6788464379](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788464379) point 1): the test does not reproduce the restart Job's `requests` (CPU 1, memory 2Gi; `charts/parchwell/templates/jobs.yaml` lines 87 to 89), only its limits.
- DA2, DA3 and DA4 are accepted by name for the trial only ([6769742939](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769742939)); this test records the DA2 interval and does not test DA3 or DA4 on AKS.
- DA7 (part 2 delivers the Job names and bindings) is accepted by name ([6774981258](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6774981258) Q6).

Runner assumptions, each checked by the test itself (it fails if false): Docker can pull the pinned broker image and the SDK can pull the pinned base image; the runner gives the container a cgroup v2 namespace whose `memory.events.local` production capture can read (R5); the hook loads inside the image (R4); `CLOCK_MONOTONIC` is "A nonsettable system-wide clock" ([clock_gettime(2)](https://man7.org/linux/man-pages/man2/clock_gettime.2.html), read 2026-11-11), so parent and child ticks compare (R8 rejects negative values).

## 8. Acceptance commands

Run by the implementer before opening the pull request, in its Linux workspace with Docker:

1. The build and run steps of section 4.5, by hand, with the three variables set: the test passes and the evidence file holds one line matching section 4.6. Run 3 times (fixed count); record each line in the receipt. A failure is recorded with its message and is a finding for the receipt; the implementer does not change an assertion to pass.
2. Each mutation of section 6, once: the test fails with the named requirement. A mutation under which the test passes is recorded as a finding in the receipt, and the implementer stops and hands off under section 10; it does not weaken or remove the mutation.
3. `./scripts/verify-dotnet.sh`: exit 0, with the new test passing in the LoadGen report with the variables unset.
4. `bash infra/scripts/lifecycle/check-offline.sh`: exit 0.

After Hari merges: Hari runs the workflow on `main`; its log line of section 4.6 is the trial-gate evidence (decision 6787880185 point 2). No agent dispatches it.

## 9. Open questions for Hari

None.

## 10. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope: any name, variable, setting, line format or literal in sections 3 and 4, any file outside 2.1, or any file in 2.2. In particular:

- An observed exit code other than 137 (decision 6787880185 point 4): the production literal is outside this pull request.
- The hook cannot load inside the image, production cgroup capture fails in the container, or the parent is not PID 1 under the settings of section 4.2: each means the decided mechanism does not run as planned.
- The implementer's workspace cannot build or run the image test, so acceptance command 1 cannot run before merge.
- A merged change (section 2.4) alters a pod setting, file name, record kind or stage name this test copies or asserts.

## 11. Plan-check questions

- (a) Effort scaling with a number unknown at planning time: none. Acceptance runs are fixed at 3, mutations at 3, one workflow run per trial by Hari.
- (b) Stricter than the parents: R4 (image binaries, uid and PID 1 proven by hook records), R6's byte equality between the left-behind file and the published journal, R7's stage order, R2's precondition checks, R5's named exit-code failure and R13's output tail. Each turns a decided assumption into an observation or a named failure; none changes a design rule. R10 matches decision 6787880185 point 4's scope.
- (c) Dependencies neither on `main` nor in the pull request: the Docker daemon and image registries on the runner and in the workspace, and the runner kernel's cgroup v2 layout; each is checked by the test (section 7). The container runtime and seccomp profile difference is DA-iii, accepted by name. The restart Job's pod settings are on `main` (`charts/parchwell/templates/jobs.yaml`); part 2 may change them (section 2.4).

## 12. Sources

- Code on `main` at `3a4ee0b1`: `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs`, `B10AbruptParent.cs` (line 25), `B10AbruptProducer.cs` (`RunControlAsync`, `AdmitKillOriginalAsync`), `B10AbruptJournal.cs` (lines 50 to 61 and 80), `B10AbruptOptions.cs`, `B10AbruptProcess.cs`, `ChildCommand.cs`, `B10AbruptDiagnostics.cs` (`B10AbruptTimeline`); `src/Parchwell.LoadGen/Parchwell.LoadGen.csproj`; `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptEndToEndTests.cs`, `B10AbruptRealProcessTests.cs`, `B10AbruptProcessHarness.cs`, `PublishedApphostStartupHook.cs`; `tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs`; `charts/parchwell/templates/jobs.yaml`; `infra/scripts/phase/phase.py`; `infra/scripts/lifecycle/check-offline.sh`; `scripts/verify-dotnet.sh`, `scripts/verify-worker-images.sh`, `scripts/dotnet-verification.proj`; `.github/workflows/dotnet-verification.yml`, `verifier-c5-mutation.yml`; `.github/CODEOWNERS`.
- `items/404/execution-loadgen-receipt.md` on `main`: part 1's local observations (exit 137 and the control-cut interval), all in an agent workspace.
- Decisions of section 1; pages of sections 4.4 and 7.

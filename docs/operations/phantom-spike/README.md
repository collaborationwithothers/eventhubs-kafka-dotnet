# Phantom mechanism spike

This fixture investigates whether Toxiproxy can cause a real failed Confluent.Kafka delivery callback while an independent consumer reads the same accepted produce attempt. It runs against a local Kafka broker and establishes no Event Hubs behavior. These instructions describe the [approved issue #52 experiment][plan]; a measured finding requires the retained experiment evidence.

## Prerequisites and execution

Use Linux with Bash, a reachable Docker daemon and .NET SDK 10.0.402 on `PATH`. The repository [SDK selection][sdk] disables roll-forward and selects Microsoft Testing Platform. Install prerequisites separately; the [verification entry point][verification] checks the selected SDK and does not install tools.

Check the daemon before planning a container run:

```bash
docker info
dotnet --version
```

A successful `docker info` establishes daemon access, not successful image pulls or broker startup. Missing Docker, startup failure or incomplete experimental evidence must fail visibly rather than skip the experiment, as the [approved plan][plan] requires.

From the repository root, run the complete .NET verification:

```bash
./scripts/verify-dotnet.sh
```

The script performs locked restore, Release build, architecture checks and the nonempty Kafka test project. Its test-report validator rejects skipped tests and requires passing results; see the [foundation verification contract][foundation]. The experiment checks its evidence rather than requiring a phantom: a complete scoped negative finding is an accepted experimental result.

After the full command has built the Release outputs, rerun the Kafka test project with a fresh evidence directory:

```bash
export PARCHWELL_SPIKE_EVIDENCE_DIRECTORY="$(mktemp -d "${TMPDIR:-/tmp}/parchwell-phantom-spike.XXXXXXXX")"
dotnet test \
  --project tests/Parchwell.Kafka.Tests/Parchwell.Kafka.Tests.csproj \
  --configuration Release \
  --no-build \
  --minimum-expected-tests 1
```

This uses the repository's [Microsoft Testing Platform invocation][verification] and reruns the complete Kafka test project. It does not replace the full repository verification or its XML report validation.

## Evidence retention

`PARCHWELL_SPIKE_EVIDENCE_DIRECTORY` selects the experiment evidence directory. Full verification assigns a directory below its temporary verification output and prints the actual path. It also prints the experiment summary and self-contained trial JSON to the verification log on success or failure. Preserve that output together with the detailed traces when investigating a run; these are [approved implementation boundaries][plan], not evidence of a successful experiment.

Routine execution writes evidence outside tracked repository files. Detailed local traces are temporary; these instructions make no claim that hosted runner files survive job teardown. A durable finding belongs under this directory with its measured evidence, source commit, dirty-state declaration and reproduction command. It does not belong under `results/` and is not a scenario verdict. These boundaries follow the [approved plan][plan] and [verdict-integrity rules][rules].

## Frozen experiment

The [approved plan][plan] uses generic Testcontainers to start both containers. The producer connects through Toxiproxy, including connections discovered through Kafka metadata. Separate direct broker access serves the admin client and independent observer. Routing and fault activation require recorded evidence before a trial can contribute to a complete matrix.

Use these approved image pins. Their source is the corresponding Dockerfile in Testcontainers 4.16.0:

- Kafka: `confluentinc/cp-kafka:7.5.12@sha256:997883c60e6f1207fb5f779c7a8fdae57312a17f446fd5ea3eb8b73f52d4a1cf`. [Kafka image source][kafka-image].
- Toxiproxy: `ghcr.io/shopify/toxiproxy:2.12.0@sha256:9378ed52a28bc50edc1350f936f518f31fa95f0d15917d6eb40b8e376d1a214e`. [Proxy image source][proxy-image].

Compare explicit `retries=0` with explicit `retries=2147483647`, the default documented in [librdkafka 2.16.0 configuration][config]. Keep `acks=all`, `enable.idempotence=false` and transactions off. Enable delivery reports, their persistence-status field, and producer statistics/debug evidence. The [managed producer source][producer] substitutes `PossiblyPersisted` when the status field is disabled, so that substitute cannot serve as independent persistence evidence.

For each retry setting, use the following approved configurations:

| Fault | Direction and attributes |
|---|---|
| None | Healthy control |
| `latency` | Downstream; no jitter; delay exceeds the local message timeout |
| `timeout` | Downstream; `timeout=0`; retain until the terminal callback or safety deadline |
| `reset_peer` | Downstream; `timeout=0`; retain until the terminal callback or safety deadline |

These are TCP faults, not selectors for Kafka acknowledgements. The pinned [Toxiproxy documentation][toxics] defines their behavior: latency delays data; a zero timeout drops data until removal without closing the connection; reset_peer resets connections immediately with a zero timeout.

Initial approved timing estimates, dated 2026-10-14, are `message.timeout.ms=5000`, `socket.timeout.ms=2000` and downstream latency `10000` ms. Basis: a bounded local experiment without broker runtime measurements. The planned trial deadline is 60 seconds and the overall deadline is 30 minutes including startup. These are experimental budgets, not measured performance claims. [Approval and basis][plan].

Measure healthy controls first. If those settings cannot support healthy controls, permit the single approved recalibration from measured controls, record and freeze its timings, and repeat the complete matrix within the same overall deadline. Retain superseded runs separately. Further tuning, a larger budget, new faults, idempotence variants or different broker/client versions require Hari's decision. [Approved calibration boundary][plan].

For every trial, use a fresh producer, topic, instance ID, attempt ID and proxy state. Warm metadata and demonstrate a healthy produce/consume path before fault activation. Apply the fault before the target `Produce` and retain it until the terminal callback or safety deadline. Do not cancel a produce task or remove the fault to manufacture its delivery outcome. Retain every trial, including startup, evidence and cleanup failures. [Approved trial procedure][plan].

The approved repeat rule requires at least five independent trials per configuration. This is an acceptance rule, not a measured reliability or statistical-confidence claim. One witnessed pair establishes existence. A recipe must reproduce the pair in every planned trial before it becomes a candidate for a future positive regression test; measured reproduction rates remain separate from existence. [Repeat approval][plan], [factory template][factory].

## Findings and completion

Record the mechanism finding independently from matrix completion and experimental integrity:

- **Yes:** A genuine failed delivery callback for an accepted `Produce` attempt and independent consumption of that exact instance ID and attempt ID, with matching key and payload. A witnessed pair can establish existence while other trials remain incomplete; incomplete execution still fails the characterization checks.
- **No:** The complete frozen matrix has verified routing, active faults where applicable and complete evidence, without a witnessed pair. State "not reproduced in the tested configurations" within the bounded observation period. Do not claim impossibility or permanent absence of a later append.
- **Inconclusive:** Startup/tooling failure, unverified routing or faults, missing/conflicting evidence, or an incomplete matrix without a witnessed pair.

These criteria are [Hari's approved decisions][plan]. Persistence status alone, task cancellation, a global client error or a synthetic failed row cannot establish the pair. Callback and consumed-record observations must remain attributable to the exact target attempt; preserve synchronous rejection separately from acceptance.

The failed callback invalidates authoritative full-run classification under [ADR-013][adr-013]. The spike retains diagnostic evidence, not authoritative `ok`, `loss`, `phantom` or `reported_failure` totals. [ADR-012][adr-012] defines the identity join; [ADR-016][adr-016] distinguishes component evidence from full-run validity. Cleanup and safety-stop evidence cannot be silently replaced by passing status.

A scoped no requires a separate Hari-reviewed ADR-016 revision merged before verifier step 7. Inconclusive also blocks that step until the assumption is resolved. Steps 4-6 may continue under their own approved plans. The spike does not substitute synthetic phantom evidence or revise the ADR itself. [Approved consequence][plan].

## Dependencies and review

The approved direct additions are Confluent.Kafka 2.16.0 and generic Testcontainers 4.16.0, confined to the existing Kafka test project. The approved native transitive pin is librdkafka.redist 2.16.0. Their exact manifests are [Confluent.Kafka][kafka-package], [Testcontainers][containers-package] and [librdkafka.redist][native-package]. The complete proposed transitive approval list is in the [issue plan][plan]; it was inferred from manifests, not established by that planning session's restore.

Compare the actual restored graph against the approved additions and existing locked dependencies. Any additional package or different version requires Hari's approval. Existing `xunit.v3.mtp-v2` stays at 4.0.2. No Testcontainers.Kafka, Testcontainers.Toxiproxy or Toxiproxy.Net package is approved; `HttpClient` controls toxics. Versions belong in central package management and resolved graphs in affected lock files, following the [dependency rules][foundation] and [approved plan][plan].

The implementation must follow the [.NET engineering standards][dotnet] and retain the package-approval exception explicitly in its PR. Run documentation checks and a fresh cold review before each push; Hari retains the merge decision for the approved new dependencies. See [AGENTS.md review rules][rules] and the [repository cold-review skill][review].

[plan]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/52#issuecomment-6172812371
[sdk]: ../../../global.json
[verification]: ../../../scripts/verify-dotnet.sh
[foundation]: ../../engineering/dotnet-verification.md
[rules]: ../../../AGENTS.md#before-opening-a-pull-request
[factory]: ../../agents/factory.md#plan-template
[dotnet]: ../../engineering/dotnet.md
[review]: ../../../.agents/skills/repo-review/SKILL.md
[adr-012]: ../../blueprint.md#adr-012-the-oracle-is-a-per-message-ledger-joined-into-four-outcome-classes
[adr-013]: ../../blueprint.md#adr-013-a-run-is-the-window-between-high-watermarks-recorded-before-and-after-the-load-generator
[adr-016]: ../../blueprint.md#adr-016-component-tests-cause-outcomes-for-real-with-testcontainers-kafka-and-toxiproxy-loss-is-simulated-and-says-so
[kafka-image]: https://github.com/testcontainers/testcontainers-dotnet/blob/4.16.0/tests/Testcontainers.Kafka.Tests/Dockerfile
[proxy-image]: https://github.com/testcontainers/testcontainers-dotnet/blob/4.16.0/tests/Testcontainers.Toxiproxy.Tests/Dockerfile
[config]: https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md
[toxics]: https://github.com/Shopify/toxiproxy/blob/v2.12.0/README.md#toxics
[producer]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Producer.cs
[kafka-package]: https://api.nuget.org/v3-flatcontainer/confluent.kafka/2.16.0/confluent.kafka.nuspec
[containers-package]: https://api.nuget.org/v3-flatcontainer/testcontainers/4.16.0/testcontainers.nuspec
[native-package]: https://api.nuget.org/v3-flatcontainer/librdkafka.redist/2.16.0/librdkafka.redist.nuspec

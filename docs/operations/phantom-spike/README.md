# Phantom mechanism spike

Yes. In the measured local experiment, Toxiproxy caused genuine failed Confluent.Kafka delivery callbacks while an independent consumer read the same accepted attempts. The complete approved matrix produced 30 witnessed mechanism pairs across 40 trials; these remain diagnostic evidence under ADR-013 and establish no Event Hubs behavior. [Decision][measured-decision], [trial records][measured-trials], [approved scope][plan].

## Measured finding

The tested source was [9643aa9ec6ece739fecefd30b2146bb1564cb8fd](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/commit/9643aa9ec6ece739fecefd30b2146bb1564cb8fd), with `Dirty=false`. The experiment ran from `2026-10-14T17:38:03.2286942+00:00` to `2026-10-14T17:43:10.3206978+00:00` UTC on 2026-10-14. All planned trials completed, with experimental integrity established and no recorded integrity failures. [Measured source, timestamps and observations][measured-trials], [decision][measured-decision].

The recipe table below was generated from `decision.json`; it reports observed reproduction in this run, not a guarantee about later runs.

| Retries | Fault | Completed trials | Witnessed pairs | Future positive-test candidate |
|---|---|---|---|---|
| `0` | `None` | 5/5 | 0/5 | No |
| `0` | `latency` | 5/5 | 5/5 | Yes |
| `0` | `timeout` | 5/5 | 5/5 | Yes |
| `0` | `reset_peer` | 5/5 | 5/5 | Yes |
| `2147483647` | `None` | 5/5 | 0/5 | No |
| `2147483647` | `latency` | 5/5 | 5/5 | Yes |
| `2147483647` | `timeout` | 5/5 | 5/5 | Yes |
| `2147483647` | `reset_peer` | 5/5 | 5/5 | Yes |

All fault recipes meet the approved candidate gate for a future positive regression test. The healthy controls have successful callbacks and no witnessed failed-callback pairs. Actual producer records report managed version `2.16.0+56ef87df3402861f0dc72ef921fcfac702ae1f16` and native librdkafka `2.16.0`. The Kafka project [test report][measured-report] records 28 passed tests, with no failed or skipped tests. [Recipe decision][measured-decision], [callback/consumption evidence][measured-trials].

Retained evidence consists of [full raw experiment JSON, gzip-compressed][measured-raw], the [decision][measured-decision], [compact trial records][measured-trials] and [test report][measured-report]. The full record includes raw producer statistics/debug traces and broker/proxy logs. Authoritative verifier outcome totals are absent. A yes does not require the ADR-016 revision reserved for a scoped no; the full-run invalidity rule remains unchanged. [Approved consequences][plan], [ADR-013][adr-013], [ADR-016][adr-016].

Earlier inconclusive runs remain intact as diagnostic evidence:

- [Initial listener validation][earlier-listener] rejected the proxy state before target production. The fixture's guard was corrected to accept Toxiproxy's wildcard-address normalization from `0.0.0.0` to `[::]`. [Current endpoint validation][infrastructure].
- [Docker create timeout][earlier-docker] failed during broker creation. The timeout wrapper now preserves detected authentication while bounding Docker requests with the original experiment deadline. The legacy `CleanupSucceeded=true` records the then-tracked resource cleanup path; it does not establish the absence of a late-created orphan. [Current startup/cleanup handling][infrastructure], [timeout configuration][docker-authentication].
- [Administration setup timeout][earlier-admin] timed out creating a topic during healthy controls. The setup ceiling now follows the original trial deadline. [Current administration bound][observer].

These earlier records are superseded, not negative reproduction results. Their placeholder trial entries are not observed attempts or empirical reproduction rates. The historical JSON has not been rewritten to apply the later fixture's improved evidence contract.

The exact orphan container `c96c38188129` was manually removed with `docker rm -fv`, which exited successfully; its removal timestamp was not recorded. [Cleanup recovery evidence][cleanup-recovery].

The [cached-image snapshot][cached-images] records local cached image identities for the configured Kafka and Toxiproxy references. The [live-container snapshot][live-images] covers Toxiproxy and the Testcontainers resource reaper, Ryuk; it does not include a live Kafka image inspection. Configured `ImagePins`, cached image identity and observed running-container identity therefore remain distinct.

## Prerequisites and execution

Use Linux with Bash, a local Docker daemon and .NET SDK 10.0.402 on `PATH`. The fixture binds published control, producer and observer ports to host loopback and connects through loopback; remote Docker hosts are unsupported. See the [container lifecycle][infrastructure]. The repository [SDK selection][sdk] disables roll-forward and selects Microsoft Testing Platform. Install prerequisites separately; the [verification entry point][verification] checks the selected SDK and does not install tools.

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

`PARCHWELL_SPIKE_EVIDENCE_DIRECTORY` selects the evidence root. When it is unset or blank, the [evidence writer][writer] uses the system temporary directory. Each run creates a fresh `phantom-spike-<GUID>` subdirectory, so records from an earlier run cannot supply the current run's result.

The writer produces these files:

| File | Contents |
|---|---|
| `experiment.json` | Full experiment record, including trial diagnostics, producer logs/statistics and broker/proxy logs when collection succeeds |
| `decision.json` | Mechanism finding, matrix completion, experimental integrity and their reasons |
| `trial-records.json` | Compact trial evidence, callback and independent-consumption records, warmup and routing/fault evidence, and configuration/source identity |

The compact projection omits raw debug/statistics and broker/proxy log streams; preserve `experiment.json` for those details. The [writer implementation][writer] defines the files and projection. A missing diagnostic remains missing evidence rather than a claimed successful collection.

Full verification assigns an evidence root below its temporary verification output and prints the actual path. It prints `decision.json` and `trial-records.json` to the job log on success or failure, while preserving test failures and XML report validation. See the [verification script][verification] and its [evidence-publishing target][report-validation]. Preserve that output together with `experiment.json` when investigating a run; successful publication is not evidence of a successful experiment.

Routine execution writes evidence outside tracked repository files. Detailed local traces are temporary; these instructions make no claim that hosted runner files survive job teardown. A durable finding belongs under this directory with its measured evidence, source commit, dirty-state declaration and reproduction command. Retain the full raw JSON, compressed losslessly if needed, alongside the decision and compact trial records; preserve earlier inconclusive evidence separately. It does not belong under `results/` and is not a scenario verdict. These boundaries follow the [approved plan][plan] and [verdict-integrity rules][rules].

## Frozen experiment

The [approved plan][plan] uses generic Testcontainers to start both containers. The producer connects through Toxiproxy, including connections discovered through Kafka metadata. Separate direct broker access serves the admin client and independent observer. Routing and fault activation require recorded evidence before a trial can contribute to a complete matrix.

Use these approved image pins. Their source is the corresponding Dockerfile in Testcontainers 4.16.0:

- Kafka: `confluentinc/cp-kafka:7.5.12@sha256:997883c60e6f1207fb5f779c7a8fdae57312a17f446fd5ea3eb8b73f52d4a1cf`. [Kafka image source][kafka-image].
- Toxiproxy: `ghcr.io/shopify/toxiproxy:2.12.0@sha256:9378ed52a28bc50edc1350f936f518f31fa95f0d15917d6eb40b8e376d1a214e`. [Proxy image source][proxy-image].

The full experiment's `ImagePins` field records configured image references. Container IDs, when collected in infrastructure diagnostics, identify container instances; they are not an independently observed image content identity. The [evidence contract][contracts] does not record a Docker image-inspection result. Keep configured pins distinct from actually observed content identities in a finding.

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

The implementation also bounds individual operations and shutdown. These are planned implementation safety budgets, dated 2026-10-14, not measured performance or additional experiment configurations:

| Operation | Planned safety bound | Implementation source |
|---|---|---|
| Individual observer watermark query and consume slice | 250 ms | [Direct observer][observer] |
| Topic-creation request/operation timeout | 60-second ceiling, bounded by the remaining trial time | [Direct observer][observer] |
| Docker HTTP request timeout | 30-minute ceiling, bounded by the original experiment token | [Docker client configuration][docker-authentication] |
| Producer metadata lookup | 5 seconds | [Producer probe][probe] |
| Diagnostic collection | 10 seconds | [Runner][runner], [container lifecycle][infrastructure] |
| Cleanup | 30 seconds | [Runner][runner], [container lifecycle][infrastructure] |

Basis: bounded synchronous polling, administration, diagnostic collection and resource release. Diagnostic and cleanup handling can continue after the experiment deadline with separate cancellation handling. Consequently, the approved experiment deadline is not a promise that process shutdown completes at that exact wall-clock time. A query timeout does not complete the observer's scan, and a cleanup timeout does not count as successful release. [Implementation][runner], [observer completion][observer].

The Docker client configuration preserves the detected endpoint and authentication. It uses the supported timeout hook before the first client for that endpoint is cached; run each experiment in a fresh test process. Startup failures retain exact owned container names and report cleanup as unestablished, allowing any late-created resource to be identified without treating missing client state as proof of deletion. [Configuration][docker-authentication], [startup handling][infrastructure].

Measure healthy controls first. The approval permits a single recalibration from measured controls, with recorded frozen timings and a complete rerun within the same experiment budget. The current [runner][runner] does not automatically retune: failed healthy controls without a trustworthy successful calibration sample stop the fault matrix and retain an inconclusive result. Retain superseded runs separately. Further tuning, a larger experiment budget, new faults, idempotence variants or different broker/client versions require Hari's decision. [Approved calibration boundary][plan].

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
[docker-authentication]: ../../../tests/Parchwell.Kafka.Tests/PhantomSpike/Infrastructure/ExperimentDockerAuthentication.cs
[sdk]: ../../../global.json
[verification]: ../../../scripts/verify-dotnet.sh
[report-validation]: ../../../scripts/dotnet-verification.proj
[writer]: ../../../tests/Parchwell.Kafka.Tests/PhantomSpike/Evidence/SpikeEvidenceWriter.cs
[contracts]: ../../../tests/Parchwell.Kafka.Tests/PhantomSpike/Contracts.cs
[runner]: ../../../tests/Parchwell.Kafka.Tests/PhantomSpike/Experiment/SpikeRunner.cs
[observer]: ../../../tests/Parchwell.Kafka.Tests/PhantomSpike/Experiment/DirectObserver.cs
[probe]: ../../../tests/Parchwell.Kafka.Tests/PhantomSpike/Experiment/ProducerProbe.cs
[infrastructure]: ../../../tests/Parchwell.Kafka.Tests/PhantomSpike/Infrastructure/BrokerEnvironment.cs
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

[measured-decision]: evidence/2026-10-14/decision.json
[measured-trials]: evidence/2026-10-14/trial-records.json
[measured-raw]: evidence/2026-10-14/experiment.json.gz
[measured-report]: evidence/2026-10-14/results.xml
[earlier-listener]: evidence/2026-10-14/initial-listener-validation.json.gz
[earlier-docker]: evidence/2026-10-14/docker-create-timeout.json.gz
[earlier-admin]: evidence/2026-10-14/admin-setup-timeout.json.gz
[cached-images]: evidence/2026-10-14/cached-image-identities.json
[live-images]: evidence/2026-10-14/live-container-images.json
[cleanup-recovery]: evidence/2026-10-14/cleanup-recovery.json

# Kafka baseline configuration

`Parchwell.Kafka` creates native Confluent.Kafka configurations for named workload roles and records each emitted setting's inventory IDs, sources and selection reason. The [approved issue plan][plan] defines the baseline contract and its deliberate omissions. Token refresh and client construction belong to later harness work.

New to Kafka? Read [Kafka configuration explained](kafka-configuration-explained.md) for the meaning, units and interactions of the baseline settings, including the deliberately omitted keys.

## Registration and construction

Register only the roles a process uses. Each registration has its own namespace and client identity; enrichment consumption also requires a stable group identity. The following identifiers are synthetic examples:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;

services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
{
    options.NamespaceName = "parchwell-synthetic";
    options.ClientId = "synthetic-loadgen-pod";
});

var baseline = provider.GetRequiredService<KafkaConfigurationFactory>()
    .CreateLoadGenProducer();
```

Named options use the role name. `ValidateOnStart` validates every registered role through the startup validator invoked by Generic Host. Validation checks namespace DNS-label syntax and rejects missing identities, whitespace and control characters; the DNS label bound comes from [RFC 1035][dns]. It does not query Azure. [Startup registration][startup], [Host invocation][host].

Required runtime inputs come from the same catalogue entries that emit them. Adding an input-backed entry therefore adds its startup requirement. The factory snapshots the string-valued options with a record copy before resolving entries; literal, input-backed and omitted definitions have separate construction methods. [Implementation][catalogue], [factory implementation][factory-code].

The other factory methods are `CreateEnrichmentProducer`, `CreateEnrichmentConsumer` and `CreateVerifierConsumer`. An unregistered role fails explicitly. Each result owns a fresh mutable native configuration and immutable `Settings`/`Omissions` snapshots. Those snapshots describe initial emission; later caller edits do not update them or other results. Scenario-delta composition belongs to reference slice step 5. [Approved API][plan].

## Source selection and conflicts

The catalogue implements ADR-015: use Microsoft's recommended or required value specifically labelled for librdkafka, then explicit pinned librdkafka defaults where it gives no labelled value. Existing ADR decisions select enrichment store-after-process, the statistics interval and the verifier overrides. Every explicit entry records its actual emitted string, workload role, frozen inventory IDs, versioned sources and selection reason. [Design authority][blueprint], [Microsoft guidance][microsoft], [native reference][native].

The complete mapping is in [BaselineSettingsCatalog.cs][catalogue]; the independent expected contract is in [ApprovedConfigurationContract.cs][contract]. Each [setting declaration][setting-keys] links to its frozen inventory entries; scenario result links remain pending until results are published. Important selections are:

| Setting | Baseline selection | Source and inventory |
|---|---|---|
| Producer `request.timeout.ms` | `60000` | Microsoft's librdkafka recommendation; A01 |
| Producer `socket.timeout.ms` | `60000` | Pinned native default and approved shared A01 C0; A01 |
| Producer `message.timeout.ms` | `300000` | Pinned native default and shared C0; A01/B10/C18 |
| `connections.max.idle.ms` | Explicit `0` | Pinned native default under ADR-015; A04 |
| Producer `enable.idempotence` | `false` | ADR-015 and pinned native default; B04 |
| Producer `allow.auto.create.topics` | `true` | Approved native producer default; D01 |
| Consumer `allow.auto.create.topics` | `false` | Native consumer default; D01 |
| `statistics.interval.ms` | `15000` | ADR-007's starting-point decision; H01/H02, plus producer A01/B10 |

Values in this table are configuration literals, not runtime measurements. Their sources are the [Microsoft table][microsoft], [pinned native reference][native], [approved shared contract][plan] and [ADRs][blueprint]. The statistics interval still needs a later callback and bridge implementation.

Microsoft's librdkafka table states a request-timeout default of `5000`, while the pinned native reference states `30000`; the recommendation remains `60000`. The native reference describes `request.timeout.ms` as broker-enforced and producer-only. Consumers receive their network `socket.timeout.ms` setting, without a producer request-timeout entry. [Microsoft][microsoft], [librdkafka v2.16.0][native].

Java idle, linger, batching and idempotence values do not govern this .NET baseline. The generic troubleshooting idle recommendation also lacks a librdkafka label, so ADR-015 selects explicit native zero. Native code runs the Azure idle heuristic only when the idle property was not modified; a later A04 delta must distinguish an omitted key from explicit zero. [Microsoft configuration][microsoft], [troubleshooting][troubleshooting], [native default-path source][native-defaults], [ADR-015][blueprint].

Managed key spelling matters: `sasl.mechanism`, `message.send.max.retries` and `acks=-1` are the selected .NET-compatible keys/values. Emit the native aliases `compression.type` and `max.in.flight` so the corresponding managed getters read the explicit values. Enrichment reset `latest` preserves the native default `largest` action and can be parsed by the managed enum getter. The metadata and independent tests record these exact emissions. [Native aliases][native], [managed getters][managed], [enum parsing][enum-parsing].

The managed delivery keys use `dotnet.producer.*`; fields `all` retain persistence status. Keep `delivery.report.only.error` absent because the .NET producer rejects its presence even when false. [Managed mapping][managed], [producer validation][producer].

## Deliberate omissions

Transactions, SAS credential settings and native OIDC client-secret settings are excluded under ADR-003/015. Consumer static membership remains unspecified for C06. The verifier fixes its group to `parchwell-verifier`, disables auto-commit, selects reset `error` and isolation `read_committed`; its offset-store and commit-interval settings remain unspecified under the assumption that it assigns explicit offsets and never stores or commits them. [ADR-003/013/015][blueprint], [approved omissions][plan].

Omissions appear in `Omissions` with null metadata values and their own disposition; no null entries are sent to native configuration. This PR supplies no token provider, refresh handler, client factory, statistics bridge or verifier algorithm. Attach the separately planned custom refresh handler before live use. [Approved scope][plan].

## Verification

Use the pinned SDK from [global.json][sdk] and run `./scripts/verify-dotnet.sh`. Full verification includes the merged phantom spike's Docker characterization test. Mapping unit tests use only configuration objects and in-process DI; a focused MTP invocation is:

```bash
dotnet test --project tests/Parchwell.Kafka.Tests/Parchwell.Kafka.Tests.csproj \
  --configuration Release --no-build --minimum-expected-tests 1 -- \
  --filter-namespace Parchwell.Kafka.Tests.Configuration
```

The namespace filter is documented by [xUnit's MTP runner][xunit]. The test fixture compares complete emitted dictionaries to independently source-cited literals, then checks role-specific metadata, deliberate omissions, startup rejection, mutation isolation and every validation/mapping branch. It does not generate expectations from the production catalogue. [Independent tests][tests], [engineering rules][engineering].

For the approved additional coverage pass, install standalone `dotnet-coverage` at the exact pin from the plan outside the repository, then invoke:

```bash
dotnet tool install dotnet-coverage --version 18.12.0 --tool-path /tmp/parchwell-coverage-tool
./scripts/verify-kafka-coverage.sh /tmp/parchwell-coverage-tool/dotnet-coverage
```

The pin is approved in the [plan][plan] and published in its [package manifest][coverage-package]. The script collects only the library with [documented coverage configuration][coverage-settings], runs mapping tests, validates the test report and rejects missing coverage or uncovered mapping lines. Reports stay in the temporary directory printed by the script. Branch completeness is established by explicit behavior assertions; reported branch metrics alone are insufficient given the [open collector issue][coverage-issue]. This focused pass supplements full verification. [CLI documentation][coverage-cli].

### Coverage scope and controls

The checker selects every reported class under `Parchwell.Kafka.Configuration`, including generated nested classes. It also reads the built Kafka assembly and its matching portable PDB, the compiler's source-location file, to derive the executable types that must appear in the report. There is no mapping-class allowlist to maintain. A newly added executable mapping type is required even if the collector omits it. [Verifier implementation][coverage-verifier].

Missing or malformed reports, absent expected types, invalid line data and uncovered mapping lines fail. Uncovered code in another feature namespace remains outside this focused gate. A rejected coverage report cannot substitute for a failed test run: the coverage script separately validates the fresh xUnit report. [Coverage entry point][coverage-script].

The regression commands require Bash, Python and ripgrep (`rg`) on `PATH`, in addition to the pinned SDK and collector. The standalone regression controls exercise the real checker with valid and deliberately damaged reports, and collect a temporary fixture containing an unexecuted mapping type. Fixture data and edited reports are synthetic controls, never coverage evidence for the library. Run after the Release solution build:

```bash
./scripts/test-kafka-coverage.sh /tmp/parchwell-coverage-tool/dotnet-coverage
./scripts/test-kafka-documentation.sh
```

The scripts identify the expected rejection reason; unrelated tool or build failures do not count as passing controls. [Coverage controls][coverage-controls], [documentation controls][documentation-controls].

### XML inventory consistency

Full verification checks the generated XML documentation after the Release build. The checker requires each literal `KafkaSettingKeys` declaration to name its Kafka key and link to exactly the union of that key's catalogue inventory IDs across all roles. Missing, duplicate, extra or mismatched declarations fail. This protects the inventory links required by the blueprint; it does not invent result links before results exist. [XML checker][xml-checker], [library documentation requirement][blueprint].

### Pending CI coverage wiring

The existing CI entry point now runs the XML inventory check. It still does not collect mapping coverage. Hari must add the following steps after `./scripts/verify-dotnet.sh` in the .NET verification job; until then, mapping coverage is a separately executed check. The collector stays outside the repository as approved in the plan. [Current workflow][workflow], [approved collector pin][plan], [tool CLI][coverage-cli].

```yaml
- name: Install mapping coverage collector
  run: dotnet tool install dotnet-coverage --version 18.12.0 --tool-path "$RUNNER_TEMP/parchwell-coverage"
- name: Verify Kafka mapping coverage
  run: ./scripts/verify-kafka-coverage.sh "$RUNNER_TEMP/parchwell-coverage/dotnet-coverage"
```

Agents cannot edit workflow files under [AGENTS.md safety rule 8][workflow-rule]. The snippet is a concrete handoff for Hari; this PR does not claim that CI coverage is enforced.

[plan]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/54#issuecomment-6174869137
[blueprint]: ../blueprint.md
[catalogue]: ../../src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs
[setting-keys]: ../../src/Parchwell.Kafka/Configuration/KafkaSettingKeys.cs
[contract]: ../../tests/Parchwell.Kafka.Tests/Configuration/ApprovedConfigurationContract.cs
[tests]: ../../tests/Parchwell.Kafka.Tests/Configuration/
[engineering]: ../engineering/dotnet.md
[sdk]: ../../global.json
[microsoft]: https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations
[troubleshooting]: https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-troubleshooting-guide
[native]: https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md
[native-defaults]: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_conf.c#L4682-L4697
[managed]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Config_gen.cs
[enum-parsing]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Config.cs#L179-L187
[producer]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Producer.cs#L726-L732
[startup]: https://github.com/dotnet/dotnet/blob/4099f83977f4d29604a9aeb971fb0a7bc1d5f699/src/runtime/src/libraries/Microsoft.Extensions.Options/src/OptionsBuilderExtensions.cs#L24-L38
[host]: https://github.com/dotnet/dotnet/blob/4099f83977f4d29604a9aeb971fb0a7bc1d5f699/src/runtime/src/libraries/Microsoft.Extensions.Hosting/src/Internal/Host.cs#L98-L99
[dns]: https://www.rfc-editor.org/rfc/rfc1035#section-2.3.4
[xunit]: https://xunit.net/docs/getting-started/v3/microsoft-testing-platform
[coverage-package]: https://api.nuget.org/v3-flatcontainer/dotnet-coverage/18.12.0/dotnet-coverage.nuspec
[coverage-cli]: https://learn.microsoft.com/en-us/dotnet/core/additional-tools/dotnet-coverage
[coverage-settings]: https://github.com/microsoft/codecoverage/blob/8df391ed291de6ae26bf688b6e591961ab5698cd/docs/configuration.md
[coverage-issue]: https://github.com/microsoft/codecoverage/issues/218
[factory-code]: ../../src/Parchwell.Kafka/Configuration/KafkaConfigurationFactory.cs
[coverage-verifier]: ../../tests/Parchwell.ArchitectureTests/Verification/KafkaCoverageVerifier.cs
[coverage-script]: ../../scripts/verify-kafka-coverage.sh
[coverage-controls]: ../../scripts/test-kafka-coverage.sh
[documentation-controls]: ../../scripts/test-kafka-documentation.sh
[xml-checker]: ../../scripts/verification/ValidateKafkaDocumentation.cs
[workflow]: ../../.github/workflows/dotnet-verification.yml
[workflow-rule]: ../../AGENTS.md#hard-safety-rules

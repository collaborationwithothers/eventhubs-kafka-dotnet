# Kafka baseline configuration

`Parchwell.Kafka` creates native Confluent.Kafka configurations for named workload roles and records each emitted setting's inventory IDs, sources and selection reason. The [approved issue plan][plan] defines the baseline contract and its deliberate omissions. Token refresh and client construction belong to later harness work.

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

The other factory methods are `CreateEnrichmentProducer`, `CreateEnrichmentConsumer` and `CreateVerifierConsumer`. An unregistered role fails explicitly. Each result owns a fresh mutable native configuration and immutable `Settings`/`Exclusions` snapshots. Those snapshots describe initial emission; later caller edits do not update them or other results. Scenario-delta composition belongs to reference slice step 5. [Approved API][plan].

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

Transactions, SAS credential settings and native OIDC client-secret settings are excluded under ADR-003/015. Consumer static membership remains unspecified for C06. The verifier fixes its group to `parchwell-verifier`, disables auto-commit, selects reset `error` and isolation `read_committed`; its offset-store and commit-interval settings remain unspecified under the assumption that it assigns explicit offsets and never stores or commits them. [ADR-003/013/015][blueprint], [approved exclusions][plan].

Omissions appear in `Exclusions` with null metadata values and their own disposition; no null entries are sent to native configuration. This PR supplies no token provider, refresh handler, client factory, statistics bridge or verifier algorithm. Attach the separately planned custom refresh handler before live use. [Approved scope][plan].

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

### Coverage checker regression examples

The checker accepts a Cobertura report for `Parchwell.Kafka` containing executable lines for every required mapping type, with positive integer hits on every reported mapping line, including compiler-generated nested types. Lines from other library features are outside this focused gate. It rejects a missing assembly or required type, an empty report, and uncovered or invalid mapping hits. The following examples use a genuine collected report as the positive control, then create synthetic copies to test the scope and rejection behavior. These copies are never coverage evidence.

Run this example with Python's standard library and the path printed by the coverage script:

```bash
python3 - /path/to/collected/coverage.xml <<'PY'
import pathlib, subprocess, sys, tempfile, xml.etree.ElementTree as ET

source = pathlib.Path(sys.argv[1])
directory = pathlib.Path(tempfile.mkdtemp(prefix="synthetic-coverage-controls-"))
def validate(path):
    return subprocess.run([
        "dotnet", "msbuild", "scripts/dotnet-verification.proj",
        "-t:ValidateKafkaCoverage", "-p:CoverageReport=" + str(path), "-nologo"
    ], capture_output=True, text=True)
assert validate(source).returncode == 0, "Positive control must pass"
for name in ["TokenRefresh", "KafkaConfigurationFactoryExtra"]:
    root = ET.parse(source).getroot()
    classes = root.find("./packages/package/classes")
    extra = ET.SubElement(classes, "class", {
        "name": "Parchwell.Kafka.Configuration." + name,
        "filename": "Synthetic/" + name + ".cs"})
    ET.SubElement(ET.SubElement(extra, "lines"), "line", {"number": "1", "hits": "0"})
    path = directory / (name + ".xml")
    ET.ElementTree(root).write(path)
    assert validate(path).returncode == 0, name + " must remain outside the mapping gate"
    print(name + ": ignored as expected")
for name in ["missing-assembly", "missing-class", "empty", "uncovered", "uncovered-generated", "invalid-hits"]:
    root = ET.parse(source).getroot()
    if name == "missing-assembly":
        root.find("./packages/package").set("name", "Synthetic.OtherAssembly")
    elif name == "missing-class":
        classes = root.find("./packages/package/classes")
        classes.remove(next(item for item in classes if item.get("name") ==
                            "Parchwell.Kafka.Configuration.KafkaSettingSource"))
    elif name == "empty":
        root = ET.fromstring("<coverage><packages /></coverage>")
    elif name == "uncovered-generated":
        generated = next(item for item in root.findall("./packages/package/classes/class")
                         if item.get("name") == "Parchwell.Kafka.Configuration.KafkaConfigurationFactory.<>c")
        generated.find("./lines/line").set("hits", "0")
    else:
        root.find("./packages/package/classes/class/lines/line").set(
            "hits", "0" if name == "uncovered" else "invalid")
    path = directory / (name + ".xml")
    ET.ElementTree(root).write(path)
    result = validate(path)
    assert result.returncode != 0 and "Kafka mapping coverage rejected:" in (
        result.stdout + result.stderr), name + " must fail for the coverage reason"
    print(name + ": rejected as expected")
print("Synthetic checker controls: " + str(directory))
PY
```

Missing or malformed files also fail report loading. A rejected report cannot substitute for a failed test run: the coverage script separately validates the fresh xUnit test report.

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

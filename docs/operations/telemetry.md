# Worker telemetry

`AddParchwellTelemetry` registers host-owned traces, metrics and logs for Azure Monitor using a supplied Entra credential. It registers worker activity and meter names but emits no worker operations by itself. Telemetry export is operational observability, not evidence of message delivery or authority for a verifier verdict.

## Registration and ownership

The worker composition root calls `services.AddParchwellTelemetry(credential, configure)` from `Parchwell.Kafka.Telemetry`. The required `TokenCredential` is supplied by that composition root using workload identity. The library creates no credential chain or Azure CLI fallback.

Set every `ParchwellTelemetryOptions` property:

| Property | Meaning |
|---|---|
| `ConnectionString` | Runtime Azure Monitor destination; non-whitespace |
| `ServiceName` | Service identity, emitted as `service.name` |
| `ServiceInstanceId` | Worker instance identity, emitted as `service.instance.id` |
| `ScenarioId` | Scenario identity, emitted as resource attribute `scenario` |
| `RunId` | Run identity, emitted as resource attribute `run` |

Identity values must be nonempty and contain no whitespace or control characters. Configuration runs once and registration copies its values. Validation occurs before distro registration and is also registered with `ValidateOnStart`; validation errors omit the connection string. The exporter owns connection-string syntax validation. Repeated registration on the same service collection throws before a second pipeline is added.

The shared resource callback adds these identities to every signal, including automatic HTTP and Azure SDK dependencies. Existing measurement tags remain unchanged. The host owns provider lifetime: keep it alive through Kafka cleanup, statistics drain and evidence publication, using the separately planned finite worker shutdown budgets. This API builds no extra service provider or private host. Export failure cannot rewrite statistics completion, persisted raw reports or verifier results.

## Signals and policy

The [checked CT plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/159#issuecomment-6385942684) defines these fixed implementation choices. The configuration uses `UseMicrosoftOpenTelemetry` with only `ExportTarget.AzureMonitor` and the supplied connection string and credential.

| Policy | Value |
|---|---|
| Tracing, metrics, logging | Enabled |
| HTTP client and Azure SDK instrumentation | Enabled |
| ASP.NET Core, SQL client, OpenAI, Semantic Kernel, Agent Framework, Agent365 instrumentation | Disabled |
| `TracesPerSecond` | `null` |
| `SamplingRatio` | `1.0F` |
| Trace-based log sampling | Disabled |
| Live metrics and performance counters | Disabled |
| Standard metrics | Enabled |
| Offline storage | Disabled |
| Logger `IncludeScopes`, `IncludeFormattedMessage`, `ParseStateValues` | `true` |

The pinned SDK's remaining batching/export behavior is retained. These settings are policy, not measured overhead or a guarantee of backend delivery. No console logger is added here. Never log connection strings, tokens, raw native statistics JSON or message payloads through the telemetry pipeline.

`ParchwellTelemetrySources.WorkerActivitySourceName` and `WorkerMeterName` both name `Parchwell.Workers`. C4 and C5 must create and emit their worker activities/instruments through these names. There is no automatic Confluent.Kafka instrumentation or per-message span wrapper in CT.

The registered meters are `Parchwell.Kafka.Statistics`, `Parchwell.Kafka.Authentication`, and `Parchwell.Workers`. Existing statistics instruments and units remain in the [statistics bridge](statistics-bridge.md#catalogue-and-metrics). Explicit histogram views for `kafka.broker.throttle.max_ms` and `kafka.broker.rtt.p99_us` retain minimum and maximum values and only `scenario`, `run`, `role`, `client_id`, and `broker` tags. There are no topic, partition or message dimensions. These histograms aggregate native window summaries; their extrema do not reconstruct a request percentile.

## Package and source pins

Read on 2026-10-24: [Microsoft enablement guidance](https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-enable) and the [Microsoft.OpenTelemetry 1.2.0 package](https://www.nuget.org/packages/Microsoft.OpenTelemetry/1.2.0). The [published nuspec](https://api.nuget.org/v3-flatcontainer/microsoft.opentelemetry/1.2.0/microsoft.opentelemetry.nuspec) identifies distro commit `f52272f8d0242f2266fc9002b1614ba2a28392f8`. At that pin, the [builder](https://github.com/microsoft/opentelemetry-distro-dotnet/blob/f52272f8d0242f2266fc9002b1614ba2a28392f8/src/Microsoft.OpenTelemetry/MicrosoftOpenTelemetryBuilderExtensions.cs), [Azure Monitor options](https://github.com/microsoft/opentelemetry-distro-dotnet/blob/f52272f8d0242f2266fc9002b1614ba2a28392f8/src/Microsoft.OpenTelemetry/AzureMonitor/AzureMonitorOptions.cs), and [customization guide](https://github.com/microsoft/opentelemetry-distro-dotnet/blob/f52272f8d0242f2266fc9002b1614ba2a28392f8/docs/customization.md) define the registration and configuration surface used here.

The distro requires Azure.Monitor.OpenTelemetry.Exporter 1.10.0 or later. Its [1.10.0 nuspec](https://api.nuget.org/v3-flatcontainer/azure.monitor.opentelemetry.exporter/1.10.0/azure.monitor.opentelemetry.exporter.nuspec) requires Azure.Core 1.63.0 or later, so CT upgrades the central direct Azure.Core pin from 1.60.0 to 1.63.0. The [Azure.Core nuspec](https://api.nuget.org/v3-flatcontainer/azure.core/1.63.0/azure.core.nuspec) identifies commit `117f784c4102d93b65978d3b3d72de36c8824105`. No authentication implementation changes accompany this dependency requirement.

The restored OpenTelemetry SDK is 1.19.1; its [nuspec](https://api.nuget.org/v3-flatcontainer/opentelemetry/1.19.1/opentelemetry.nuspec) identifies commit `5fbeba3a3d8bbd4f4235170ddeb6329fe0b8b86e`. The [shared resource callback](https://github.com/open-telemetry/opentelemetry-dotnet/blob/5fbeba3a3d8bbd4f4235170ddeb6329fe0b8b86e/src/OpenTelemetry/OpenTelemetryBuilderSdkExtensions.cs) applies to tracing, metrics and logging. [ResourceBuilder.Clear and Build](https://github.com/open-telemetry/opentelemetry-dotnet/blob/5fbeba3a3d8bbd4f4235170ddeb6329fe0b8b86e/src/OpenTelemetry/Resources/ResourceBuilder.cs) remove the detector list and later invoke its remaining detectors, respectively.

## Offline testing

`TelemetryTestCollection` disables parallelization for the `Parchwell telemetry` collection. Its fixture saves then sets both `APPLICATIONINSIGHTS_STATSBEAT_DISABLED` and `APPLICATIONINSIGHTS_SDKSTATS_DISABLED` to `true` before any registration, options resolution, exporter or provider construction. Both remain set throughout the collection. Tests dispose all providers and listeners before the fixture restores the previous environment values, including unset values.

Both switches are necessary. The exporter at commit `73d5a6f970a6b0903522c8965ec948be4bef609c` has a separate [cached customer-statistics enablement decision](https://github.com/Azure/azure-sdk-for-net/blob/73d5a6f970a6b0903522c8965ec948be4bef609c/sdk/monitor/Azure.Monitor.OpenTelemetry.Exporter/src/Internals/CustomerSdkStats/CustomerSdkStatsHelper.cs) and [process-lifetime provider registration](https://github.com/Azure/azure-sdk-for-net/blob/73d5a6f970a6b0903522c8965ec948be4bef609c/sdk/monitor/Azure.Monitor.OpenTelemetry.Exporter/src/Internals/CustomerSdkStats/CustomerSdkStatsRegistration.cs). Restoring environment values does not reset that cache. Enabled customer-statistics tests would need a fresh process. Production registration changes neither switch.

Provider tests call the internal core with a fake export transport and an ordered resource-reset hook. The hook runs after distro registration and before production identity configuration. It adds a counting sentinel detector, calls `Clear()`, and adds only `parchwell.test.resource_reset=true`. The shared production callback then adds service, instance, scenario and run. Tests resolve the actual tracer, meter and logger providers and require that marker and those production identities on each resource, with zero sentinel detector calls. No test helper recreates the production identities.

The clear prevents the distro's [Azure VM detector](https://github.com/microsoft/opentelemetry-distro-dotnet/blob/f52272f8d0242f2266fc9002b1614ba2a28392f8/src/Microsoft.OpenTelemetry/AzureMonitor/Vendoring/OpenTelemetry.Resources.Azure/AzureVMResourceDetector.cs) from invoking its [separate metadata HTTP client](https://github.com/microsoft/opentelemetry-distro-dotnet/blob/f52272f8d0242f2266fc9002b1614ba2a28392f8/src/Microsoft.OpenTelemetry/AzureMonitor/Vendoring/OpenTelemetry.Resources.Azure/AzureVmMetaDataRequestor.cs). A fake exporter transport alone would not intercept it. Production retains distro/default resource discovery; these offline tests deliberately omit its environment and cloud attributes.

A test EventListener starts before SDK registration and stays active through provider disposal. It observes the Informational `System.Net.Http` `RequestStart` event, ID 1, and reads its named `host` payload as defined by [.NET v10.0.13 HttpTelemetry](https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/System.Net.Http/src/System/Net/Http/HttpTelemetry.cs). Missing hosts and every host other than `telemetry.invalid` flag failure, including `169.254.169.254`. Synthetic classifier tests exercise this guard without making a metadata request. The listener stores no URI path, headers or body.

The in-memory transport and synthetic credential prove authenticated export with `Bearer synthetic-token`; this token is a test fixture, not a credential. The tests also inspect activities, scoped structured logs, histogram extrema and authentication metrics from the same registered SDK pipeline. They do not prove Azure ingestion, resource discovery, RBAC or delivery after process exit.

## Follow-on integration

C3 supplies fixed telemetry settings for every scenario. The connection string comes from the persistent Terraform output at runtime and is never committed. C4 and C5 supply workload identity credentials, emit worker operations through the registered source, and keep providers alive through their separately bounded shutdown. They must consume the real merged API and report format; the [statistics bridge](statistics-bridge.md) records their independent broker-coverage and strict timestamp obligations. Backend ingestion, live RBAC and exporter impact on the measured system remain gate-2 validation.

## Validation for #159

The source base is `e7ebd3dd7508b8735820f45704304ea0801606dd`; implementation is on `codex/159-telemetry-statistics`. The validation below applies to the implementation tree containing this receipt. The [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/159#issuecomment-6386081813) checks Revision 3; [Hari's entry decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/159#issuecomment-6386105297) satisfies the entry prerequisite. This implementation receipt precedes cold review; no reviewed implementation head or PR evidence existed when it was authored. The orchestrator owns the next independent cold-review step.

Commands run from the repository root on 2026-10-24, with `/tmp/159-dotnet` prepended to `PATH` for the installed, pinned SDK 10.0.402:

```bash
dotnet restore Parchwell.slnx --force-evaluate
dotnet restore src/Parchwell.LoadGen/Parchwell.LoadGen.csproj -r linux-x64 -p:NuGetLockFilePath=packages.linux-x64.lock.json --force-evaluate
dotnet restore src/Parchwell.Verifier/Parchwell.Verifier.csproj -r linux-x64 -p:NuGetLockFilePath=packages.linux-x64.lock.json --force-evaluate
dotnet restore Parchwell.slnx --locked-mode
dotnet restore src/Parchwell.LoadGen/Parchwell.LoadGen.csproj -r linux-x64 -p:NuGetLockFilePath=packages.linux-x64.lock.json --locked-mode
dotnet restore src/Parchwell.Verifier/Parchwell.Verifier.csproj -r linux-x64 -p:NuGetLockFilePath=packages.linux-x64.lock.json --locked-mode
./scripts/verify-dotnet.sh
npm ci --ignore-scripts
npm run check:docs
git diff --check
git diff --name-only origin/main
git diff --numstat origin/main
```

All restores passed. A mechanical comparison of the active dependency graphs found 15 changed lock files, within the plan's 32-file inspection cap. Each affected graph resolves Azure.Core 1.63.0, Microsoft.OpenTelemetry 1.2.0, Azure.Monitor.OpenTelemetry.Exporter 1.10.0 and OpenTelemetry 1.19.1. Comparing the central pins and existing direct lock entries against the source base found only the approved package addition and Azure.Core upgrade. Archived evidence locks are unchanged. Restore logs are `/tmp/159-restore-{solution,loadgen,verifier}.log` and `/tmp/159-locked-{solution,loadgen,verifier}.log`.

Focused Release verification also passed. The new capture-completion class contributes 26 tests to the 95 passing statistics tests; the telemetry classes contribute 17 passing tests. These counts were computed from `/tmp/159-statistics-tests/results.xml` and `/tmp/159-telemetry-results/results.xml`; neither report contains failures or skipped tests. The commands were `dotnet build tests/Parchwell.Kafka.Tests/Parchwell.Kafka.Tests.csproj --configuration Release --no-restore` and `dotnet test --project tests/Parchwell.Kafka.Tests/Parchwell.Kafka.Tests.csproj --configuration Release --no-build`, with class filters `*Statistics*` and `Parchwell.Kafka.Tests.Telemetry.*`, the report directories above, and `--report-xunit-xml --report-xunit-xml-filename results.xml`. Logs are `/tmp/159-statistics-build.log`, `/tmp/159-statistics-tests.log` and `/tmp/159-telemetry-tests.log`. The focused build reported no warnings or errors.

The full verification output is `/tmp/159-verification.log`, with reports and per-project logs under `/tmp/parchwell-dotnet-verification.yfJtYoVY`. `./scripts/verify-dotnet.sh` passed: locked restore, Release build/analyzers, XML inventory, effective project manifests, all selected test projects and XML report validation, and the warning-as-error control. The report-derived total is 4245 passed, with no failures or skips. Enrichment remains an empty scaffold and contributes no claimed unit coverage.

| Report | Passed | Failed | Skipped |
|---|---|---|---|
| `Parchwell.ArchitectureTests` | 8 | 0 | 0 |
| `Parchwell.Kafka.Tests` | 349 | 0 | 0 |
| `Parchwell.LoadGen.Tests` | 312 | 0 | 0 |
| `Parchwell.Runner.Tests` | 1169 | 0 | 0 |
| `Parchwell.Scenarios.Tests` | 96 | 0 | 0 |
| `Parchwell.Verifier.Tests` | 2311 | 0 | 0 |

Each row comes from that project's `results.xml`; its `test.log` and `report.log` are in the same directory. The existing local Kafka/Toxiproxy experiment passed and retained its synthetic diagnostic evidence under `Parchwell.Kafka.Tests/spike/phantom-spike-7bc5159044e34bf9bbad9d5ec15cfac4`. Its outcome is not a live Event Hubs result.

`npm ci --ignore-scripts` and the documentation scan passed. The scan reports 36 advisory warnings grouped in three unchanged archived issue-114 source documents: `long.md` (9), `tolong-function.md` (13), and `union-operator.md` (14), under `docs/operations/evidence/issue-114/raw/review-10-fixes/`. These are missing upstream-relative links and ASCII punctuation in archived material, outside CT's scope. The group count is below the plan's manual-inspection cap; neither changed runbook has a warning. The full scan is `/tmp/159-check-docs-staged.log`. Whitespace checks pass. The path comparison finds 29 changed paths, all within the checked scope; its local records are `/tmp/159-changed-paths.txt` and `/tmp/159-numstat.txt`.

These tests use synthetic input and the existing local Kafka experiment. No Azure token, metadata request or live workflow dispatch is used. Cloud resource discovery, real ingestion/RBAC, worker/chart wiring and measured exporter impact remain the stated follow-on gaps. Raw test output and downloaded packages are not committed.

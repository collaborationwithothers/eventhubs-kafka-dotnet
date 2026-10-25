# .NET engineering standards

Binding for all .NET code in this repository. Status: approved by Hari on 2026-10-12. Amended in the pull request for issue #85 (2026-10-18): the mutation row of "Testing" and the boundary-test rule under it.

These rules exist because the earlier portfolio repo's code was hard to test and repeated itself: two interfaces in the whole source tree, concrete stores injected directly, and `new SqlConnection(connectionString)` in ten places. Rules here are enforced by the build wherever a tool can enforce them.

## Solution layout

```
src/
  Parchwell.Kafka/            shared library: Event Hubs client configuration, Entra token refresh,
                              statistics bridge, graceful shutdown
  Parchwell.Scenarios/        scenario definition model, loader and resolver (ADR-014)
  Parchwell.LoadGen/          service, vertical slices
  Parchwell.Enrichment/       service, vertical slices
  Parchwell.Verifier/         service, vertical slices; the oracle (protected path)
tests/
  <Project>.Tests/            one test project per source project
  Parchwell.ArchitectureTests/
scenarios/<ID>/               scenario definition and expected outcome, per inventory item
scenarios/profiles/           workload profiles shared by several items (ADR-014)
```

## Vertical slices

- Inside each service, one folder per feature: the handler, its request and response types, validation, and its own data access live together.
- A slice does not call another slice's handler. Shared behaviour moves to the service's `Common/` folder only when two slices need it, not in advance.
- `Parchwell.Kafka` is not a slice. It is a library with a small public surface, and stricter rules: every public member documented, full unit-test coverage of configuration mapping, no dependency on any service project.

## Seams and dependencies

Process and Kubernetes API boundaries, including the test-only worker-process exception, are owned by [docs/blueprint.md ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates).

- Put interfaces at our own boundaries: clock, blob store, metrics sink, scenario source, token provider. Not around types we own that have one implementation and no test need.
- Do not wrap Confluent.Kafka in our own abstraction. It is the thing under test. Use its `IProducer<TKey, TValue>` and `IConsumer<TKey, TValue>` interfaces directly where a seam is needed.
- Construction goes through dependency injection. No `new` of a service or client outside a composition root or a factory registered in DI.
- Configuration goes through the options pattern with validation on start (`ValidateOnStart`). A missing setting fails at start, not at first use.
- Time comes from `TimeProvider`, never `DateTime.UtcNow`, so time-dependent logic is testable.

## Correctness settings (Directory.Build.props)

- `TargetFramework` net10.0.
- `Nullable` enable.
- `TreatWarningsAsErrors` true.
- `AnalysisLevel` latest-recommended, with .NET analyzers enforced in build.
- Central package management (`Directory.Packages.props`), exact versions. Confluent.Kafka pinned exactly; every result records the version used.

## Testing

| Layer | What it tests | Tooling |
|---|---|---|
| Unit | Slice handlers, configuration mapping, verifier logic, scenario loading | xUnit, no I/O |
| Component | A service against a local Kafka broker in a container | Testcontainers, with Toxiproxy for faults (ADR-016); tests harness logic only, never Event Hubs behaviour |
| Architecture | Dependency direction and slice isolation | NetArchTest or ArchUnitNET, in `Parchwell.ArchitectureTests` |
| Mutation | Verifier judgement logic: checks, join, window and probe gate (#85 decision 4) | Stryker.NET; minimum mutation score to be set with the reference slice |
| Live | Event Hubs behaviour | Scenario workflow only; never in CI |

Rules:
- A behaviour change and the test that proves it land in the same pull request.
- Never loosen, skip or delete an assertion in the same pull request as the code it tests. If an assertion is wrong, fix it in its own pull request with the reason.
- Every comparison and boundary in verifier code has tests at the boundary, just below it and just above it, and every empty input has a test (#85 decision 4).
- Test names state the behaviour: `Given_<state>_When_<action>_Then_<outcome>`.

## Architecture tests (examples, finalised with the reference slice)

- `Parchwell.Kafka` depends on no `Parchwell.*` project.
- No service depends on another service.
- No type in one slice folder references a type in another slice folder of the same service.
- No production type references `DateTime.UtcNow` or `DateTime.Now`.

## Logging and telemetry

- Structured logging with message templates. Log messages are for operators: what happened, with identifiers. No explanations of Kafka concepts.
- Metric names follow docs/blueprint.md section 10 (`kafka.*` from the statistics bridge, `parchwell.*` for our own).
- Every log and metric carries scenario ID and run ID.

## What reviewers should not flag

Style the analyzers enforce. Duplication that exists in only two places and is not yet a pattern; extract on the third.

## Kubernetes workers

The #138 deployment boundary is owned by [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates). Use SDK container publishing without Dockerfiles, full source-commit image tags and recorded immutable digests. Package the scenario and profile files; each worker resolves its selected configuration in-process through Parchwell.Scenarios. There is no generated comparison document. Each worker's `ContainerBaseImage` is an official image, pinned by digest, that contains every shared framework listed in the worker's published `runtimeconfig.json`. LoadGen and Verifier use `mcr.microsoft.com/dotnet/aspnet`, because `Parchwell.Kafka` references Microsoft.OpenTelemetry, whose [1.2.0 manifest](https://api.nuget.org/v3-flatcontainer/microsoft.opentelemetry/1.2.0/microsoft.opentelemetry.nuspec) depends on OpenTelemetry.Instrumentation.AspNetCore, whose [1.19.0 manifest](https://api.nuget.org/v3-flatcontainer/opentelemetry.instrumentation.aspnetcore/1.19.0/opentelemetry.instrumentation.aspnetcore.nuspec) references the `Microsoft.AspNetCore.App` framework ([#179](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/179)). The [worker image smoke](dotnet-verification.md#worker-image-smoke) checks that each image starts.

Job settings, producer registration and last-action completion receipts follow [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates). Preserve scoped workload identities and the declared LoadGen/Verifier node placement. A01 runs without default-deny NetworkPolicies or service-tag policy inputs under the existing [#109 removal decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/109#issuecomment-6358477325). Private-endpoint IP outputs remain deployment data. Child plans state finite worker and Job bounds; these execution bounds do not change scientific verdict rules.

Telemetry placement, fixed chart configuration and exporter participation follow [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates). Required CI combines unit tests, rendered Helm validation with kubeconform, actionlint and shellcheck under the adopted [#138 requirement 22](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6382769026); the live smoke gates establish infrastructure behavior.

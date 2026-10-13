# .NET engineering standards

Binding for all .NET code in this repository. Status: approved by Hari on 2026-10-12.

These rules exist because the earlier portfolio repo's code was hard to test and repeated itself: two interfaces in the whole source tree, concrete stores injected directly, and `new SqlConnection(connectionString)` in ten places. Rules here are enforced by the build wherever a tool can enforce them.

## Solution layout

```
src/
  Parchwell.Kafka/            shared library: Event Hubs client configuration, Entra token refresh,
                              statistics bridge, graceful shutdown
  Parchwell.Scenarios/        scenario definition model, loader and Helm values generator (ADR-014)
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
| Mutation | Verifier logic | Stryker.NET; minimum mutation score to be set with the reference slice |
| Live | Event Hubs behaviour | Scenario workflow only; never in CI |

Rules:
- A behaviour change and the test that proves it land in the same pull request.
- Never loosen, skip or delete an assertion in the same pull request as the code it tests. If an assertion is wrong, fix it in its own pull request with the reason.
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

# eventhubs-kafka-dotnet

> **Status: blueprint stage (October 2026).** No scenario has been run yet. The results table below fills in only as scenarios are measured. Nothing in this repo is a measurement until it appears in `results/` with its method.

Microsoft documents how Kafka clients should be configured for Azure Event Hubs. This repo tests that guidance against a real Event Hubs Premium namespace, using the Confluent.Kafka .NET client. For each item it records what the documentation says, what actually happens, what breaks when the guidance is ignored, how the failure shows up in a .NET service, and how an operations team detects it.

The workload is synthetic. It models Parchwell, a fictional UK legal SaaS company that processes documents for law firms.

## Results

| ID | Guidance | Observed | Verdict | Details |
|---|---|---|---|---|
| | | | | |

Verdicts: confirmed, contradicted, undocumented, untestable.

## Read next

- [docs/blueprint.md](docs/blueprint.md): thesis, architecture, decisions, failure modes, the frozen inventory of 66 items, and the estimate.

<!-- Ruleset test. Do not merge. -->

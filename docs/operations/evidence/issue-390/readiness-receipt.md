# Partition-add readiness receipt

The component test observes the added partition and its leader before sending its synthetic record. Its subject remains verifier handling of an established added partition, with successful production and every existing assertion required.

## Execution card

- Deliverable and exact changes: [plan Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/390#issuecomment-6672078583), approved by the fresh-context [standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/390#issuecomment-6672131429), with no implementation corrections.
- Authorization: [issue scope](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/390), [Hari's readiness decision, point 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6671722866), and the user's instruction to implement as Codex and finish ready for review. The issue also records [Tool: Codex](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/390#issuecomment-6671956988).
- Mandatory gates: plan acceptance steps 1-6, unchanged successful produce/persistence and verifier assertions, applicable cold review, final-head CI and hosted Codex review. No merge or workflow dispatch.
- Verification: the exact commands in plan acceptance steps 1-5, reproduced below with measured results after execution.
- Earliest publication checkpoint: required local acceptance and a complete applicable cold review, then draft PR for independent CI and hosted review.
- Review budget: docs/agents/pull-requests.md rule 8, at most two comprehensive and two focused rounds cumulatively; on exhaustion or unsafe focus, save unpublished work to the branch with `-wip` appended and hand off to Hari. No prior implementation or cold-review rounds exist for this PR.
- Other workload bounds and exhaustion actions: plan acceptance, including the fixed regression sample and changed-file warning disposition cap. No failed positive invocation may be replaced by a passing retry.

## Sources and specification coverage

Baseline: `3d21683cdd95cdb5d74bf532f5785e29b1a5370a` on main. Changes are confined to the planned component test, item state and this authored receipt. No production verifier, expected outcome, result, mutation target or workflow changes.

The current test's start capture, partition addition, produce and verifier result match the subject stated in the plan. The inserted precondition checks topic and partition existence without errors and a leader matching a returned broker. Readiness is observed within the chosen total/query/delay/attempt limits; failed metadata observations are retained and exhaustion throws explicitly. The awaited produce and its `Persisted` assertion are unchanged. Metadata observation does not guarantee delivery.

API claims were checked against Confluent.Kafka 2.16.0, tag commit `56ef87df3402861f0dc72ef921fcfac702ae1f16`: [IAdminClient](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/IAdminClient.cs), [Metadata](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/Metadata.cs), [TopicMetadata](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/TopicMetadata.cs), [PartitionMetadata](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/PartitionMetadata.cs), [BrokerMetadata](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/BrokerMetadata.cs) and [Error](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/Error.cs). The chosen budgets are test setup limits, not performance measurements or exact scheduling guarantees.

Applied docs/blueprint.md ADR-013 and ADR-016, docs/engineering/dotnet.md, AGENTS.md governance/review rules and docs/agents/pull-requests.md. No test assertion is loosened, removed or skipped.

Predicate map: not applicable to verifier judgment logic, which is unchanged. The positive partition-add proof covers established readiness followed by successful production and end-only-partition verification. The missing-partition control covers explicit readiness exhaustion and diagnostic target/bound/observation text. No new abstraction around the Kafka client is introduced.

## Verification

Source commit: `3d21683cdd95cdb5d74bf532f5785e29b1a5370a`, plus the planned working-tree changes. The tested component source blob is `3f669f67086c3fb6a986e50a0a2f8853eb35056e`. SDK 10.0.402 was installed separately; Docker Server 28.4.0 was available. All following commands ran from the repository root with that SDK on PATH.

```bash
dotnet restore Parchwell.slnx --locked-mode
dotnet build Parchwell.slnx --configuration Release --no-restore
```

Both passed; the build reported no warnings or errors. A script compared the original target method against main after removing only the new readiness call: exact equality. It also required exact equality of the original `ProduceAsync` method and the rest of the file after it.

The five fresh-process target commands and one missing-partition command are the exact commands in plan acceptance steps 2-3. Their actual results directory was `/tmp/partition-readiness.XXSGYS8E`; each subdirectory contains `results.xml`, with the process log beside it. An XML parser required the named single test, one `Pass`, no failures/skips and readiness output in each report; every positive report additionally had to contain `ready=True`. All six invocations passed on their first execution. No failed test was retried.

Each positive observation reported topic and partition error `NoError`, partition IDs `[0,1]`, leader `1`, broker IDs `[1]`, and `leaderPresent=true`, on the first metadata query. Each then passed the unchanged producer persistence and verifier assertions. The missing-partition control observed partition IDs `[0]`, `partitionPresent=false` and `leaderPresent=false`, then passed its assertions requiring `TimeoutException` and target/bound/last-observation diagnostics. This chosen sample does not establish a failure rate or prove flakiness eliminated.

A preliminary SDK help-discovery command failed in the SDK help handler before test execution; a subsequent `--list-tests` command with the target filter exited successfully and discovered exactly the named test. This was a runner-option probe, separate from the regression sample. No implementation acceptance failure has occurred.

`./scripts/verify-dotnet.sh` passed, including fresh report validation, architecture gates, locked restore/build, Kafka XML inventory consistency and the intended warning-as-error negative control. Its log/report directory is `/tmp/parchwell-dotnet-verification.dL1HzDZO`. XML parsing independently found 8880 passed tests, zero failed and zero skipped:

| Project | Passed |
| --- | --- |
| Parchwell.ArchitectureTests | 15 |
| Parchwell.Enrichment.Tests | 56 |
| Parchwell.Kafka.Tests | 486 |
| Parchwell.LoadGen.Tests | 1180 |
| Parchwell.Scenarios.Tests | 336 |
| Parchwell.Verifier.Tests | 6807 |

The full verifier report includes both the original partition-add proof and the new missing-partition control. Raw logs and machine-readable reports stay outside tracked files. Final staged documentation scan: `npm run check:docs`; existing warnings in unchanged files are left as advisory under docs/agents/pull-requests.md rule 8. No warning disposition is required for changed files.

## Cold review

Prepared mode: comprehensive. Input revision will be the staged tree against the baseline merge base. Cumulative rounds before this review: zero comprehensive, zero focused. The complete report and final-head CI will be linked in the PR description; the report will not be added to its own reviewed input.

Models: Codex authoring session (model identifier not exposed), high-effort Codex planning and independent plan-check subagents; high-effort Codex cold reviewer (model identifier not exposed).

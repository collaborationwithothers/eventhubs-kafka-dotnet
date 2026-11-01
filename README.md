# eventhubs-kafka-dotnet

> Results are published below as items are completed. Tested results include their method; traced items are marked untestable and include their reason.

Microsoft documents how Kafka clients should be configured for Azure Event Hubs. This repo tests that guidance against a real Event Hubs Premium namespace, using the Confluent.Kafka .NET client. For each item it records what the documentation says, what actually happens, what breaks when the guidance is ignored, how the failure shows up in a .NET service, and how an operations team detects it.

The workload is synthetic. It models Parchwell, a fictional UK legal SaaS company that processes documents for law firms.

## Results

| ID | Guidance | Observed | Verdict | Details |
|---|---|---|---|---|
| A01 | Timeout behaviour under throttling | No positive throttle sample in the valid synthetic probe; the full timeout and duplicate workloads were gated off. | untestable | [Report](results/A01/2026-10-25.md) |
| A02 | TCP keepalive configuration | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| A03 | Metadata refresh interval | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| A04 | Idle connection configuration | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| A05 | Kafka ports and network rules | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| A06 | Private DNS resolution | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| B01 | Oversize event rejection on Premium | A 1,100,000-byte synthetic event failed after peer resets; a 1,000-byte control delivered. The exact size boundary was not tested. | confirmed | [Report](results/B01/2026-10-27.md) |
| B02 | Oversize batches | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| B03 | Compression support on Premium | Gzip delivered; snappy, lz4 and zstd were rejected. Throughput and compression ratios were not tested. | confirmed | [Report](results/B03/2026-10-27.md) |
| B04 | Idempotent producer support on Premium | Producing with idempotence enabled succeeded without fatal client errors or reorder. Retry duplicate suppression and restart behaviour were not tested. | confirmed | [Report](results/B04/2026-10-26.md) |
| B05 | Transaction initialization on Premium | InitTransactions returned without error. Only initialization was tested; full transactions and exactly-once semantics were not. | confirmed | [Report](results/B05/2026-10-27.md) |
| B06 | Acknowledgement settings | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| B07 | Batching delay | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| B08 | Partitioner compatibility | The default `consistent_random` partitioner sent the key `b08-synthetic-0` to partition 30 and `murmur2_random` sent it to partition 2 in all five repeats of each, on a 32-partition event hub, matching the partitions computed from the pinned librdkafka source. One key and one partition count were tested. | confirmed | [Report](results/B08/2026-11-01.md) |
| B09 | Produce throttling | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| B10 | Message timeout and application retry | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| B11 | Claim-check ordering and retention | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| C01 | Session timeout configuration | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| C02 | Heartbeat interval configuration | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| C03 | Long processing and poll interval | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| C04 | Rebalancing without application errors | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| C05 | Fetch throttling | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| C06 | Static group membership | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| C07 | Partition assignment strategy | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| C08 | KIP-848 consumer protocol | Not supported on our Event Hubs Premium namespace on 2026-10-28 with librdkafka 2.16.0 (inferred from pod logs). C1's approved wording is "no assignment and no error within the window"; three repeats remain incomplete with SASL failures of unknown cause. Published under Hari's C08-only waiver. | undocumented | [Report](results/C08/2026-10-28.md) |
| C09 | Offset commit throttling | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| C10 | Offset storage and crash outcomes | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| C11 | Trace: offset metadata size | Not run: the pinned client exposes no commit metadata. | untestable | [Report](results/C11/2026-10-27.md) |
| C12 | Consumer group ID length on Premium | A 256-character ASCII group.id obtained assignments; 257 characters were rejected. Only subscribe was tested; non-ASCII IDs were not. | confirmed | [Report](results/C12/2026-10-27.md) |
| C13 | Consumer group quota | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| C14 | Zombie consumer membership | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| C15 | Offset retention and reset | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| C16 | Trace: concurrent receive request quota | Not run: requires client counts beyond build scale. | untestable | [Report](results/C16/2026-10-27.md) |
| C17 | Poison messages and dead-letter handling | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| C18 | Graceful shutdown | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| C19 | Noisy tenant effects | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| D01 | Kafka topic management APIs | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| D02 | Adding partitions | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| D03 | Namespace partition quota | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| D04 | Trace: brokered connection quota | Not run: requires connection counts beyond build scale. | untestable | [Report](results/D04/2026-10-27.md) |
| D05 | Trace: event hub quota | Not run: documentation trace only. | untestable | [Report](results/D05/2026-10-27.md) |
| E01 | Tombstone handling | A produce with a null value read back as null, and a produce with an empty value read back as empty, in all five repeats each, after a non-null seed on the same key and partition. Only transport on one partition was tested; compaction, retention and deletion were not. | confirmed | [Report](results/E01/2026-11-01.md) |
| E02 | Values between compaction runs | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| E03 | Tombstone retention and offline consumers | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| E04 | Adding partitions to a compacted hub | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| E05 | Trace: compacted partition capacity | Not run: beyond build scale. | untestable | [Report](results/E05/2026-10-27.md) |
| E06 | Legal hold race | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| F01 | Workload identity authentication | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| F02 | Built-in IMDS authentication | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| F03 | Token refresh failures | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| F04 | Missing-role and wrong-resource-token errors | Missing-role produce returned broker error 29; assigned consume had "no read and no error within the window", establishing neither denial nor success. A wrong-resource token returned local error -169. | undocumented | [Report](results/F04/2026-10-28.md) |
| F05 | Local authentication rejection | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| F06 | Identity role scope | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| G01 | Planned geo-replication promotion | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| G02 | Forced geo-replication promotion | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| G03 | Replication lag and throttling | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| G04 | Trace: regional private DNS zones | Not run: the harness is single-region. | untestable | [Report](results/G04/2026-10-27.md) |
| G05 | Metadata-only disaster recovery | Unpublished (out of scope). | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| H01 | Trace: throttling visibility in ThrottledRequests | Not run: without a throttle event there was nothing to detect. | untestable | [Report](results/H01/2026-10-27.md) |
| H02 | Consumer lag measurements | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| H03 | Trace: UnknownServerException | Not run: cannot be triggered deliberately. | untestable | [Report](results/H03/2026-10-27.md) |
| H04 | Older Kafka clients | Unpublished (out of scope). | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| H05 | AMQP-encoded headers | Unpublished (out of scope). | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| H06 | Basic tier authorization errors | Unpublished (out of scope). | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| H07 | Workload saturation point | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| H08 | Scheduled re-validation startup | Unpublished. | - | [Inventory](docs/blueprint.md#frozen-inventory) |
| K01 | Worker probes | Unpublished. | - | [Inventory](docs/blueprint.md#inventory-amendment-1-approved-by-hari-2026-10-12) |
| K02 | CPU limits and client behaviour | Unpublished. | - | [Inventory](docs/blueprint.md#inventory-amendment-1-approved-by-hari-2026-10-12) |
| K03 | Native memory and pod limits | Unpublished. | - | [Inventory](docs/blueprint.md#inventory-amendment-1-approved-by-hari-2026-10-12) |
| K04 | DNS lookup and re-resolution | Unpublished. | - | [Inventory](docs/blueprint.md#inventory-amendment-1-approved-by-hari-2026-10-12) |
| K05 | Scaling and group rebalances | Unpublished. | - | [Inventory](docs/blueprint.md#inventory-amendment-1-approved-by-hari-2026-10-12) |
| K06 | Trace: spot eviction and termination grace | Not run: the harness uses normal pools. | untestable | [Report](results/K06/2026-10-27.md) |
| K07 | Trace: zone spread of consumer members | Not run: a zone outage cannot be induced. | untestable | [Report](results/K07/2026-10-27.md) |
| K08 | Trace: VM freeze pauses | Not run: a platform freeze cannot be triggered. | untestable | [Report](results/K08/2026-10-27.md) |
| K09 | Trace: LocalDNS prerequisites and effects | Not run: LocalDNS changes the DNS path for every item. | untestable | [Report](results/K09/2026-10-28.md) |
| K10 | Trace: SNAT use for Entra egress | Not run: exhaustion needs scale. | untestable | [Report](results/K10/2026-10-27.md) |

Verdicts: confirmed, contradicted, undocumented, untestable.

Rows marked unpublished have no published report. A dash means no published verdict.

## Read next

- [Kafka configuration explained](docs/operations/kafka-configuration-explained.md): a beginner's guide to each baseline setting, its units and how settings interact.
- [Kafka baseline configuration](docs/operations/kafka-baseline.md): register workload roles, understand value selection and run verification.
- [docs/blueprint.md](docs/blueprint.md): thesis, architecture, decisions, failure modes, the frozen inventory of 66 items plus amendment 1 (10 Kubernetes items), and the estimate.
- [docs/engineering/dotnet-verification.md](docs/engineering/dotnet-verification.md): reproduce the .NET foundation build, architecture gates and approved controls.

# Draft: process lifecycle, container and Kubernetes rules

Not binding. Held for the code half, where these rules are designed against the real manifests and workload identities and then moved into docs/engineering/dotnet.md. Each rule cites a row in docs/research/kubernetes-kafka-clients.md.

## Open questions raised by Codex on PR #6 (decisions for Hari)

1. **Egress scope (P0).** Kubernetes NetworkPolicy cannot name Entra or Azure Monitor, so the draft allows HTTPS 443 to any external address. That is an exfiltration path. Options: FQDN-based policy (Cilium FQDN filtering, if the cluster's networking supports it), an egress proxy, or Azure Monitor Private Link Scope for telemetry plus a decision on Entra.
2. **CoreDNS selection (P1).** With Azure CNI powered by Cilium, an `ipBlock` cannot select pod or node IPs; DNS egress should use namespace and pod selectors, with LocalDNS handled separately if used.
3. **Verifier lock identity (P1).** A blob lease needs blob write permission; ADR-003 grants the verifier only Event Hubs Data Receiver. Either add a narrowly scoped Blob data role on a lock container or choose a lock the existing identity can take.

## Draft rules

Binding for every service image. Each rule cites the row in docs/research/kubernetes-kafka-clients.md that justifies it. Inventory amendment 1 tests the Kafka-specific consequences; these rules keep our own harness from producing false results.

- **Generic Host.** Every service runs on the .NET Generic Host. .NET 10 has no default SIGTERM handler outside it (K-01).
- **Exec-form `ENTRYPOINT`.** The app is PID 1 and receives SIGTERM directly; no shell wrapper (K-02).
- **Exit codes.** Any failure that ends a run sets a non-zero exit code explicitly, including a background service that throws and a fatal Kafka error. On .NET 10 the host otherwise exits with 0, and a Kubernetes Job would record a failed run as a success (K-07, K-54). A test proves each failure path exits non-zero.
- **One instance per run.** Kubernetes can start a Job's program twice at once (K-08). Every producer stamps each message and ledger line with its instance ID (pod name plus start time). The verifier marks the run invalid if it sees more than one load generator instance for a run ID, and a verifier refuses to start if another verifier already holds the run's lock in Blob storage (a blob lease on the run ID).
- **Stop token.** Background work honours the host's stop token. `Consume` is called with a timeout or the token, never blocking indefinitely (K-06).
- **Shutdown budget.** `terminationGracePeriodSeconds` is set explicitly and is longer than `HostOptions.ShutdownTimeout` plus any preStop time. Both values are recorded in every evidence pack (K-03).
- **Shutdown order.** Orchestrated explicitly by one coordinating hosted service, never left to registration order, which no primary source guarantees (K-58): consumers stop before the producers they feed. Producers `Flush` within the remaining budget; consumers `Close`, never only `Dispose` (K-04, K-05). `Close` commits only when auto-commit is enabled. Every consumer sets `enable.auto.offset.store=false` and stores an offset only after its message is processed (ADR-005); with `enable.auto.commit=false` it then commits those stored offsets explicitly before `Close`, so shutdown never commits past an unprocessed message (K-56). The only exception is C10's deliberately unsafe configuration, which says so in its `scenario.yaml`.
- **Probes.** A startup probe gates liveness. Liveness reports whether the process can make progress, never whether lag or processing time is high (K-11, K-12). The harness's probe settings are fixed; K01 tests the alternatives.
- **Resources.** CPU and memory requests and limits are set explicitly per service and recorded in every evidence pack, with `Environment.ProcessorCount` as seen by the process (K-15). The memory limit allows for librdkafka's native queues outside the GC heap (K-16 to K-18).
- **GC mode.** The GC mode is set explicitly in the project file and recorded in every evidence pack (K-19).
- **Client identity.** `client.id` is the pod name, injected through the Downward API (K-51). `group.instance.id`, where used, comes from a stable StatefulSet ordinal, never a Deployment pod name (K-23).
- **Statistics.** librdkafka statistics are on in every service, bridged to OpenTelemetry (K-52, ADR-007).
- **Network policy.** Kubernetes NetworkPolicy matches IP blocks, not names (K-34, K-43). Egress is allowed by IP block to CoreDNS (53 UDP and TCP), the Event Hubs private endpoint on 9093 and the Blob private endpoint on 443. Entra and Azure Monitor ingestion (the destinations in docs/networking/hub-firewall.md section 6) have no fixed IPs, so pods may also reach HTTPS 443 outside the cluster and spoke ranges. Restricting that to named destinations needs FQDN filtering or an egress firewall, both deferred (section 12). The node's IMDS endpoint (169.254.169.254, HTTP 80) is denied by default; F02 allows it as an explicit configuration in its `scenario.yaml`, because F02 tests what `azure_imds` does when it is reachable (K-49). The evidence pack records the policy actually applied.


# Kafka clients on Kubernetes against Event Hubs: research catalogue

This catalogue lists 58 practices and failure modes at the boundary between a librdkafka-based Kafka client (Confluent.Kafka on .NET), Kubernetes on AKS, and Azure Event Hubs' Kafka endpoint. Every row cites a primary source; rows marked "unconfirmed" have none and are therefore test questions, not claims. The "Disposition" column says how each row enters the v1 inventory under inventory amendment 1 (docs/blueprint.md section 12).

Status: research fetched 2026-10-12. Disposition proposed 2026-10-12, pending Hari's approval of amendment 1.

## How to read this

- **Confidence.** "confirmed": the cited primary source states the fact. "confirmed*": every cited fact is confirmed, but the combined consequence is an inference that a test would establish. "unconfirmed": no primary source found.
- **Disposition.** "fold X": becomes a configuration or hypothesis of existing item X. "new KNN": a new inventory item. "rule": an engineering rule for this repo's own harness (docs/engineering/), plus a section in docs/kubernetes.md citing the source; not a test. "guide": a docs/kubernetes.md section only.
- **Versions.** Sources S05 to S08 were read at the pinned tags librdkafka v2.16.0 and Confluent.Kafka v2.16.0. S01 to S04 and S09 to S11 were read from `master` and are re-checked at the pinned tag before any claim built on them ships.

## Findings that change existing items

1. **A04.** librdkafka sets its Azure default `connections.max.idle.ms` of 230000 ms only when `bootstrap.servers` contains the string "azure" (S05, verified at v2.16.0). Event Hubs bootstrap addresses are `<namespace>.servicebus.windows.net:9093`, which does not contain it, so the default stays 0 (disabled).
2. **A02.** `socket.keepalive.enable` only sets SO_KEEPALIVE; librdkafka sets no keepalive timers (S06). The AKS Linux default keepalive time is 7200 s (S19), far above the 240 s Event Hubs idle cutoff (S13). Whether Event Hubs counts keepalive probes as activity is undocumented (K-37).
3. **C18 and harness integrity.** .NET 10 installs no default SIGTERM handler outside the Generic Host (S30). Before .NET 11, a background service failure that stops the host usually exits with code 0 (S34), which a Kubernetes Job records as success.
4. **F01, F03, C03.** Confluent.Kafka runs the consumer's OAUTHBEARER refresh only from inside `Consume` (S08). A consumer that stops calling `Consume` during long processing cannot refresh its token.
5. **F02.** Every pod can reach the node's IMDS by default, so `azure_imds` exercises the node's managed identity, not workload identity, unless AKS IMDS restriction is enabled (S28).

## Catalogue

| ID | Practice or failure mode | Kafka or EH specific | Reproducible at build scale | Confidence | Sources | Disposition |
|---|---|---|---|---|---|---|
| K-01 | .NET 10 has no default SIGTERM handler; without Generic Host the process dies at once (no Flush, no Close) | no | yes | confirmed | S30, S31 | rule |
| K-02 | Shell-form ENTRYPOINT runs the app under `/bin/sh -c`; it is not PID 1, gets no SIGTERM, and is SIGKILLed at the end of the grace period | no | yes | confirmed | S39, S40 | rule; fold C18 (shell-form image configuration) |
| K-03 | Default `terminationGracePeriodSeconds` (30 s) equals the default `HostOptions.ShutdownTimeout` (30 s); preStop time comes out of the same budget | no | yes | confirmed* | S40, S41, S32 | fold C18 |
| K-04 | Producer: `message.timeout.ms` default 300000 ms far exceeds a 30 s grace period; queued and in-flight messages are dropped at SIGKILL; `Flush` must fit the remaining grace | yes | yes | confirmed | S02, S01, S11 | fold C18 |
| K-05 | `Close()` commits stored offsets and leaves the group; `Dispose` alone does neither; static members never send LeaveGroup | yes | yes | confirmed | S09, S02 | fold C18, C06 |
| K-06 | The background task must honour its stop token; a blocking `Consume` without token or timeout holds the host until the shutdown timeout | no | yes | confirmed | S32 | rule |
| K-07 | A .NET 10 background service failure stops the host with exit code 0; a Job records success and does not retry | no | yes | confirmed | S33, S34, S47 | rule (harness integrity) |
| K-08 | A Job can run the same program twice at once, even with parallelism and completions at 1 | no | unsure | confirmed | S47 | fold C14 |
| K-09 | Restart back-off for Pods and Jobs; each restart of a dynamic group member triggers a rebalance | yes | yes | confirmed* | S40, S47 | fold C04 (crash-loop configuration) |
| K-10 | Ordinary containers get SIGTERM in arbitrary order; native sidecars only after main containers exit | no | yes, with a sidecar | confirmed | S40 | guide |
| K-11 | A liveness probe tied to processing progress or lag restarts consumers during long processing or rebalances | yes | yes | confirmed* | S42, S01 | new K01 |
| K-12 | A startup probe holds liveness back; first token fetch and group join happen only once `Consume` runs | yes | yes | confirmed* | S42, S08 | new K01 |
| K-13 | Readiness and `minReadySeconds` pace rollouts even without Service traffic; ready before assignment lets old pods go too fast | yes | yes | confirmed* | S45, S40 | new K01 |
| K-14 | CPU limits are enforced by CFS throttling; throttled librdkafka threads may miss heartbeats and exceed `session.timeout.ms` | yes | yes | unconfirmed (heartbeat effect) | S44 | new K02 |
| K-15 | `Environment.ProcessorCount` follows the CPU limit, rounded up; thread pool sizing follows it | no | yes | confirmed | S38 | rule; configuration of K02 |
| K-16 | Producer queue defaults (1048576 KB, 100000 messages) are native memory outside the GC heap; a smaller pod limit can be OOMKilled before QueueFull | yes | yes | confirmed* | S01, S44 | new K03 |
| K-17 | Consumer prefetch defaults (`queued.max.messages.kbytes`, `fetch.max.bytes`, `receive.message.max.bytes`) size native memory | yes | yes | confirmed | S01 | new K03 |
| K-18 | The GC heap hard limit defaults to 75 percent of the container limit and ignores librdkafka's native queues | yes | yes | confirmed* | S36, S01 | new K03 |
| K-19 | Worker apps default to workstation GC; DATAS is on by default since .NET 9; server GC is unavailable on one core | no | yes | confirmed | S37 | rule; configuration of K03 |
| K-20 | Replicas above the partition count sit idle; CPU-based HPA can add idle pods; every scale event is a rebalance | yes | yes | confirmed | S15, S56 | new K05 |
| K-21 | HPA scale-down stabilisation defaults to 5 min; a flapping metric still causes repeated rebalances unless `behavior` dampens it | yes | yes | confirmed* | S56, S02 | new K05 |
| K-22 | RollingUpdate defaults of 25 percent surge and unavailable; surge pods join while old members remain | yes | yes | confirmed* | S45, S12 | fold C06, C07 |
| K-23 | Deployment pod names change every rollout, so `group.instance.id` from the pod name is not stable; Confluent recommends StatefulSets | yes | yes | confirmed | S45, S46, S02, S12 | fold C06 |
| K-24 | A duplicate `group.instance.id` fences the older member with a fatal error | yes | yes | confirmed | S02, S47 | fold C06, C14 |
| K-25 | StatefulSet rollouts replace one ordinal at a time; a static member must return within `session.timeout.ms` | yes | yes | confirmed | S46, S13, S01 | fold C06 |
| K-26 | Moving from eager to cooperative-sticky needs two rolling restarts; mixed assignor types are rejected | yes | yes | confirmed | S12, S01 | fold C07 |
| K-27 | Confluent.Kafka's Func-returning revoked and lost handlers are not supported with cooperative rebalancing | yes | yes | confirmed | S10 | fold C07 |
| K-28 | Do not commit on partitions lost; without a lost handler the revoked handler is called instead | yes | yes | confirmed | S10 | fold C03, C18 |
| K-29 | A PDB covers voluntary evictions only; `minAvailable` equal to the replica count blocks node drains | no | yes | confirmed | S48, S49 | guide; fold C06 (node drain configuration) |
| K-30 | AKS upgrade drain timeout and surge; each drained node evicts consumers | no | yes | confirmed | S23 | fold C06 (node drain configuration) |
| K-31 | Spot eviction notice of about 30 s (two Microsoft pages disagree on "up to" versus "at least") | no | unsure (normal pools, ADR-008) | confirmed | S24, S25, S26 | new K06 (D) |
| K-32 | Spread consumers across zones with topology spread constraints | no | no | confirmed | S57 | new K07 (D) |
| K-33 | A VM freeze pauses CPU and network; a pause beyond `session.timeout.ms` causes a rebalance | yes | no | confirmed* | S25 | new K08 (D) |
| K-34 | Pods need egress to CoreDNS and to the private endpoint on 9093; default-deny egress also blocks DNS | no | yes | confirmed | S52 | rule; guide |
| K-35 | `socket.keepalive.enable` sets only SO_KEEPALIVE; AKS default keepalive time is 7200 s; pod sysctl `tcp_keepalive_time` below 240 is needed | yes | yes | confirmed | S06, S19, S53, S13 | fold A02 |
| K-36 | librdkafka's Azure idle default applies only when `bootstrap.servers` contains "azure"; Event Hubs addresses do not | yes | yes | confirmed | S05, S01, S03, S13 | fold A04 |
| K-37 | Whether the Event Hubs 240 s idle close counts TCP keepalive probes as activity | yes | yes | unconfirmed | S13 | fold A02 (hypothesis) |
| K-38 | librdkafka hides idle-classified disconnect logs; the statistics `disconnects` counter is the reliable signal | yes | yes | confirmed | S02, S04 | fold A04, H01 |
| K-39 | `ndots:5` sends the Event Hubs name through search-domain lookups first; `broker.address.ttl` 1000 ms re-resolves on reconnect | yes | yes | confirmed* | S51, S21, S01, S22 | new K04 |
| K-40 | LocalDNS needs Kubernetes 1.31 or later and 4 vCPUs, and reimages nodes | no | unsure | confirmed | S22 | new K09 (D) |
| K-41 | Public egress (Entra) uses load balancer SNAT; private endpoint traffic probably does not | no | no | confirmed (limits); unconfirmed (bypass) | S20, S29 | new K10 (D) |
| K-42 | librdkafka connects only where needed; every client counts toward brokered connections per PU | yes | yes | confirmed (client); unconfirmed (EH broker IDs) | S02, S16 | fold D04; record broker count in every evidence pack |
| K-43 | Workload identity needs HTTPS egress to `login.microsoftonline.com`, which has no fixed IPs | yes | yes | confirmed | S27, S52 | fold F03 |
| K-44 | Projected token TTL default 3600 s, refreshed by kubelet at 80 percent; re-read the token file on each exchange | yes | yes | confirmed | S18, S54, S55 | fold F01, F03 |
| K-45 | The Kafka token resource is `https://<namespace>.servicebus.windows.net` | yes | yes | confirmed | S17 | fold F01, F04 |
| K-46 | librdkafka refreshes the token at 80 percent of lifetime; on failure it keeps the old token and retries after 10 s | yes | yes | confirmed | S07 | fold F03 |
| K-47 | The consumer's refresh callback runs only from `Consume`; the producer's runs on its background poll thread | yes | yes | confirmed | S08, S01, S11 | fold F01, C03 |
| K-48 | Whether Event Hubs supports SASL re-authentication (KIP-368) or closes connections at token expiry | yes | yes | unconfirmed (EH side) | S03 | fold F03 (hypothesis) |
| K-49 | Every pod can reach the node's IMDS; `azure_imds` uses the node identity, not workload identity | yes | yes | confirmed* | S02, S28 | fold F02 |
| K-50 | librdkafka's built-in jwt-bearer grant (KIP-1139) with Entra federation is untested | yes | yes | unconfirmed | S02 | fold F02 (hypothesis) |
| K-51 | `client.id` defaults to "rdkafka"; set it from the pod name through the Downward API | yes | yes | confirmed | S01, S50 | rule; fold H02 |
| K-52 | Statistics are off by default; they expose rtt, throttle, queues, lag and rebalance counts from inside the pod | yes | yes | confirmed | S01, S04 | rule (already in ADR-007) |
| K-53 | Idempotence does not survive a pod restart; Event Hubs has no transactions | yes | yes | confirmed* | S01, S13 | fold B04, B10 |
| K-54 | A fatal producer error or fencing leaves the client unusable; exit non-zero | yes | unsure | confirmed | S02 | rule |
| K-55 | Pause and resume for long processing; keep calling `Consume` so poll interval and token refresh stay served | yes | yes | confirmed | S01, S02, S08 | fold C03 |
| K-56 | `Close` commits stored offsets; Event Hubs throttles commits to 2 per second per partition, which can slow shutdown | yes | yes | confirmed* | S09, S02, S14 | fold C18, C09 |
| K-57 | A consumer killed before its first auto-commit restarts with no committed offset; `auto.offset.reset` `largest` skips silently | yes | yes | confirmed | S01 | fold C15, C10 |
| K-58 | Hosted services stop in reverse registration order unless configured otherwise | no | yes | confirmed | S32, S35 | rule |

## Not found in a primary source

- Whether the Event Hubs 240 s idle close counts TCP keepalive probes (K-37).
- Whether Event Hubs supports KIP-368 re-authentication (K-48).
- An explicit Event Hubs statement that static membership and LeaveGroup are supported.
- How many broker IDs Event Hubs advertises (K-42).
- Any primary source linking CPU throttling to librdkafka heartbeats (K-14).
- Whether private endpoint traffic uses load balancer SNAT ports (K-41).
- Whether Entra federation works with librdkafka's jwt-bearer grant (K-50).
- How a trailing-dot bootstrap name interacts with Event Hubs TLS.
- librdkafka thread count per client.
- Kafka client behaviour during Event Hubs service maintenance.

## Sources

All fetched on 2026-10-12.

- S01: https://github.com/confluentinc/librdkafka/blob/master/CONFIGURATION.md
- S02: https://github.com/confluentinc/librdkafka/blob/master/INTRODUCTION.md
- S03: https://github.com/confluentinc/librdkafka/blob/master/CHANGELOG.md
- S04: https://github.com/confluentinc/librdkafka/blob/master/STATISTICS.md
- S05: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_conf.c#L4682-L4687
- S06: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_transport.c#L1046-L1052
- S07: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_sasl_oauthbearer.c#L430-L432
- S08: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Consumer.cs
- S09: https://github.com/confluentinc/confluent-kafka-dotnet/blob/master/src/Confluent.Kafka/IConsumer.cs
- S10: https://github.com/confluentinc/confluent-kafka-dotnet/blob/master/src/Confluent.Kafka/ConsumerBuilder.cs
- S11: https://github.com/confluentinc/confluent-kafka-dotnet/blob/master/src/Confluent.Kafka/IProducer.cs
- S12: https://docs.confluent.io/platform/current/clients/consumer.html
- S13: https://learn.microsoft.com/azure/event-hubs/apache-kafka-configurations
- S14: https://learn.microsoft.com/azure/event-hubs/apache-kafka-troubleshooting-guide
- S15: https://learn.microsoft.com/azure/event-hubs/event-hubs-scalability#partitions
- S16: https://learn.microsoft.com/azure/event-hubs/azure-event-hubs-apache-kafka-overview
- S17: https://learn.microsoft.com/azure/event-hubs/authenticate-application
- S18: https://learn.microsoft.com/azure/aks/workload-identity-overview
- S19: https://learn.microsoft.com/azure/aks/improve-network-fault-tolerance-in-aks-using-tcp-keepalive
- S20: https://learn.microsoft.com/azure/aks/configure-load-balancer-standard
- S21: https://learn.microsoft.com/azure/aks/coredns-troubleshoot
- S22: https://learn.microsoft.com/azure/aks/dns-concepts
- S23: https://learn.microsoft.com/azure/aks/upgrade-options
- S24: https://learn.microsoft.com/azure/aks/spot-node-pool
- S25: https://learn.microsoft.com/azure/aks/node-auto-drain
- S26: https://learn.microsoft.com/azure/architecture/guide/spot/spot-eviction
- S27: https://learn.microsoft.com/azure/aks/outbound-rules-control-egress
- S28: https://learn.microsoft.com/azure/aks/imds-restriction
- S29: https://learn.microsoft.com/troubleshoot/azure/azure-kubernetes/create-upgrade-delete/error-code-invalidloadbalancerprofileallocatedoutboundports
- S30: https://learn.microsoft.com/dotnet/core/compatibility/core-libraries/10.0/sigterm-signal-handler
- S31: https://learn.microsoft.com/dotnet/core/extensions/generic-host#host-shutdown
- S32: https://learn.microsoft.com/aspnet/core/fundamentals/host/generic-host#shutdowntimeout
- S33: https://learn.microsoft.com/dotnet/core/compatibility/core-libraries/6.0/hosting-exception-handling
- S34: https://learn.microsoft.com/dotnet/core/compatibility/extensions/11/ihost-runasync-stopasync-throw-backgroundservice-failure
- S35: https://learn.microsoft.com/dotnet/core/compatibility/extensions/10.0/backgroundservice-executeasync-task
- S36: https://learn.microsoft.com/dotnet/core/runtime-config/garbage-collector
- S37: https://learn.microsoft.com/dotnet/standard/garbage-collection/workstation-server-gc
- S38: https://learn.microsoft.com/dotnet/core/compatibility/core-libraries/6.0/environment-processorcount-on-windows
- S39: https://docs.docker.com/reference/dockerfile/#entrypoint
- S40: https://kubernetes.io/docs/concepts/workloads/pods/pod-lifecycle/
- S41: https://kubernetes.io/docs/concepts/containers/container-lifecycle-hooks/
- S42: https://kubernetes.io/docs/concepts/workloads/pods/probes/
- S44: https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/
- S45: https://kubernetes.io/docs/concepts/workloads/controllers/deployment/
- S46: https://kubernetes.io/docs/concepts/workloads/controllers/statefulset/
- S47: https://kubernetes.io/docs/concepts/workloads/controllers/job/
- S48: https://kubernetes.io/docs/concepts/workloads/pods/disruptions/
- S49: https://kubernetes.io/docs/tasks/run-application/configure-pdb/
- S50: https://kubernetes.io/docs/concepts/workloads/pods/downward-api/
- S51: https://kubernetes.io/docs/concepts/services-networking/dns-pod-service/
- S52: https://kubernetes.io/docs/concepts/services-networking/network-policies/
- S53: https://kubernetes.io/docs/tasks/administer-cluster/sysctl-cluster/
- S54: https://kubernetes.io/docs/tasks/configure-pod-container/configure-service-account/
- S55: https://kubernetes.io/docs/concepts/storage/projected-volumes/
- S56: https://kubernetes.io/docs/concepts/workloads/autoscaling/horizontal-pod-autoscale/
- S57: https://kubernetes.io/docs/concepts/scheduling-eviction/topology-spread-constraints/

S43 is unused.

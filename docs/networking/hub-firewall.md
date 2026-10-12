# Running this design behind a hub firewall

Status: documentation only. Written 2026-10-11. Not built or tested in v1.

The v1 harness runs in a single spoke VNet with no hub firewall. Many enterprises put every workload spoke behind a hub with Azure Firewall and force all egress through it. This page lists what changes in that topology for a .NET Kafka client talking to Event Hubs Premium over a private endpoint, and which firewall rules are needed.

Every rule below comes from the Microsoft or GitHub documentation cited at the end. None has been validated against this repo's harness. Where the sources disagree, or where the consequence for Kafka is inference, this page says so.

## Assumed topology

- A hub VNet with Azure Firewall, DNS proxy enabled.
- A workload spoke holding the AKS cluster and the private endpoints for Event Hubs and Blob, peered to the hub.
- A user-defined route `0.0.0.0/0 -> firewall private IP` on the AKS node subnet, with AKS `outboundType = userDefinedRouting`.
- Spoke VNet DNS servers set to the firewall's private IP.
- A runner subnet (GitHub-hosted runners with Azure private networking) either in the hub or in another spoke, also routed through the firewall.

## The three findings that matter most

1. **Kafka cannot use application rules.** Application rules filter HTTP, HTTPS and MSSQL only (F1, F2). Kafka on port 9093 is TLS but not HTTPS, so it needs a network rule. Network rules do not SNAT traffic to private addresses by default (F3). If Kafka traffic to the private endpoint is routed through the firewall, Microsoft says SNAT must be configured to keep the flow symmetric (F4).
2. **The firewall adds its own idle timeout, and Microsoft's pages disagree on its value.** One page says east-west traffic has no idle timeout (F5). Another says east-west is fixed at 5 minutes and that no TCP reset is sent when it fires (F6). Event Hubs already closes idle connections after 240 s (E1). A silent drop with no reset is the kind of failure that surfaces in a Kafka client as a timeout long after the connection died (inference).
3. **Firewall scale-in and maintenance cut long-lived connections.** On scale-in, instances drain for 90 seconds, then reset remaining sessions. Microsoft recommends bidirectional TCP keepalive at 30-second intervals (F6). Kafka clients hold long-lived connections, so `socket.keepalive.enable = true` (already required by Event Hubs guidance, E1) becomes doubly important. Whether librdkafka's keepalive interval meets the 30-second recommendation depends on the node's kernel settings, not librdkafka (inference; to verify).

## Rules by traffic flow

### 1. Kafka clients to the Event Hubs private endpoint

**Path without the firewall.** If the private endpoints sit in the same spoke as AKS, the private endpoint's /32 system route is more specific than the `0.0.0.0/0` UDR, so the traffic goes direct and the firewall never sees it (inference from route precedence; Microsoft notes UDRs are bypassed for private endpoint traffic unless overridden, F4). No rule is needed.

**Path through the firewall.** Only if policy requires inspecting this traffic:
- Enable network policies on the private endpoint subnet, so UDRs apply to it (F4, F7).
- Route the private endpoint subnet's prefix, or each /32, to the firewall (F4).
- Network rule: source AKS node subnet, destination private endpoint IP, TCP 9093.
- Configure the firewall to SNAT private ranges for this traffic (F3, F4).
- An FQDN in a network rule needs DNS proxy, does not support wildcards, and filters by resolved IP rather than SNI (F1, F2).

### 2. Claim-check blobs (HTTPS 443)

Application rule for the storage account's blob FQDN, or the private endpoint if routed through the firewall. Application rules always SNAT, which is why Microsoft prefers them for private endpoint traffic (F4).

### 3. Kafka over the public endpoint (not used in v1)

If a team does not use private endpoints: outbound TCP 9093 to the namespace, plus TCP 443. Microsoft states 443 is required even when the main protocol is on another port, because token acquisition from Entra runs over HTTPS (E2). This is north-south traffic, so the 4-minute default idle timeout applies (F5, F6).

### 4. Workload identity token exchange

Application rule: `login.microsoftonline.com`, HTTPS 443 (A1).

### 5. AKS cluster operation

Required, from Microsoft's AKS egress reference (A1). UK South values shown:

| Rule type | Destination | Port | Purpose |
|---|---|---|---|
| Application | `*.hcp.uksouth.azmk8s.io` | HTTPS 443 | Node to API server. Konnectivity uses ALPN; TLS inspection that rewrites ALPN breaks it |
| Application | `mcr.microsoft.com`, `*.data.mcr.microsoft.com`, `mcr-0001.mcr-msedge.net` | HTTPS 443 | System images |
| Application | `management.azure.com` | HTTPS 443 | Azure API |
| Application | `login.microsoftonline.com` | HTTPS 443 | Entra |
| Application | `packages.microsoft.com` | HTTPS 443 | Packages |
| Application | `acs-mirror.azureedge.net`, `packages.aks.azure.com` | HTTPS 443 | Kubernetes and CNI binaries |
| Network | firewall private IP (custom DNS) | TCP and UDP 53 | DNS must work over both protocols |

Optional: Ubuntu security update FQDNs on HTTP 80 and `snapshot.ubuntu.com` on 443 (A1). Without them, nodes only get OS fixes through node image upgrades.

### 6. Telemetry to Azure Monitor

From the AKS egress reference's Azure Monitor section (A1):

- Network rule: service tag `AzureMonitor`, TCP 443.
- Application rules: `*.in.applicationinsights.azure.com` (narrow to the ingestion endpoint in the connection string), `dc.services.visualstudio.com`, `*.ods.opinsights.azure.com`, `*.oms.opinsights.azure.com`, `*.monitoring.azure.com`, `global.handler.control.monitor.azure.com`, `uksouth.handler.control.monitor.azure.com`, `*.ingest.monitor.azure.com`, `login.microsoftonline.com`.

### 7. Container images for this repo's workloads

Depends on the registry chosen, which the blueprint has not yet fixed (open item). For Azure Container Registry: the registry's login server and its regional data endpoint, or a private endpoint.

### 8. GitHub runner with Azure private networking

- GitHub recommends allow-listing the domains published on its meta endpoint (`https://api.github.com/meta`, `domains.actions` and related sections) (G1).
- Outbound traffic from the runner subnet must not be subject to TLS interception (G1). On Azure Firewall Premium, exclude the runner subnet from TLS inspection.
- The runner also needs whatever the workflow calls. For this repo, by inference from the workflow design: `management.azure.com` and `login.microsoftonline.com` for Terraform and Azure CLI; the Terraform registry and HashiCorp release endpoints for providers; `api.nuget.org` for builds; the AKS API server FQDN for Helm and kubectl. Confirm each against the firewall's deny logs on first run rather than trusting this list.

### 9. DNS

- With DNS proxy, the firewall resolves on the clients' behalf. Spoke VNets point their DNS at the firewall's private IP (F1, F8).
- The firewall must be able to resolve `privatelink.servicebus.windows.net` and `privatelink.blob.core.windows.net`, so those private DNS zones must be linked to the VNet the firewall resolves from, or reached through a DNS Private Resolver (inference from F8 and how private DNS zones resolve).
- DNS proxy caches positive answers up to 1 hour and negative answers up to 30 minutes (F8). A private endpoint recreated with a new IP during a test cycle could be served a stale answer for that long (inference).

### 10. SNAT capacity for public egress

Each firewall public IP gives 2,496 SNAT ports per backend instance; attaching NAT Gateway to the firewall subnet raises this (F1). Kafka to a private endpoint is not SNATed by default and does not consume these ports (F3). Only public egress does.

## Candidate inventory items for v1.1

These would extend the frozen inventory if hub firewall testing is ever added:

1. Kafka connection idle through the firewall east-west for over 5 minutes: drop behaviour and what librdkafka reports, given that no reset is sent.
2. Firewall scale-in during a sustained Kafka load: reconnect behaviour, duplicates, and rebalances.
3. Network rule without private-range SNAT for private endpoint traffic: whether flow asymmetry shows up as connection failures.
4. DNS proxy negative caching after a private endpoint is recreated.

## Sources

| ID | Source |
|---|---|
| A1 | [Outbound network and FQDN rules for AKS clusters](https://learn.microsoft.com/en-us/azure/aks/outbound-rules-control-egress) |
| E1 | [Apache Kafka client configurations for Azure Event Hubs](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations) |
| E2 | [Event Hubs FAQ: What ports do I need to open on the firewall?](https://learn.microsoft.com/en-us/azure/event-hubs/event-hubs-faq#what-ports-do-i-need-to-open-on-the-firewall) |
| F1 | [Azure Firewall and traffic inspection (design guide)](https://learn.microsoft.com/en-us/azure/networking/design-guide/azure-firewall) |
| F2 | [Use FQDN filtering in network rules](https://learn.microsoft.com/en-us/azure/firewall/fqdn-filtering-network-rules) |
| F3 | [Azure Firewall FAQ: SNAT between private networks](https://learn.microsoft.com/en-us/azure/firewall/firewall-faq) |
| F4 | [Azure Firewall scenarios to inspect traffic destined to a private endpoint](https://learn.microsoft.com/en-us/azure/private-link/inspect-traffic-with-azure-firewall) |
| F5 | [TCP idle timeout behavior and long-running TCP sessions in Azure Firewall](https://learn.microsoft.com/en-us/azure/firewall/long-running-sessions) |
| F6 | [Understanding Azure Firewall TCP session management and idle timeout behavior](https://learn.microsoft.com/en-us/azure/firewall/tcp-session-behavior) |
| F7 | [Secure traffic destined to private endpoints in Azure Virtual WAN](https://learn.microsoft.com/en-us/azure/firewall-manager/private-link-inspection-secure-virtual-hub) |
| F8 | [Azure Firewall DNS settings](https://learn.microsoft.com/en-us/azure/firewall/dns-settings) |
| G1 | [About Azure private networking for GitHub-hosted runners](https://docs.github.com/en/organizations/managing-organization-settings/configuring-private-networking-for-github-hosted-runners-in-your-organization) |

All read 2026-10-11.

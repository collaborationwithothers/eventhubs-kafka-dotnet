# Terraform foundations

The persistent root owns shared network, identities, registry and monitoring. The cluster root owns reusable AKS; the ephemeral root owns run resources. Lifecycle ownership follows [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates) and the [Terraform engineering standard](../engineering/terraform.md).

The [checked Terraform roots plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/157#issuecomment-6385941149), with its [ownership and cleanup decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/157#issuecomment-6386075427), defines this interface. [Terraform engineering standards](../engineering/terraform.md) own execution, tagging and backend rules; [the blueprint](../blueprint.md#adr-011-terraform-with-azure-verified-modules-where-they-cover-the-need) owns module selection and documented fallbacks.

## Private deployment inputs

Supply values through the protected deployment environment. The following table lists names and purposes, not deployment values. Set `TF_VAR_persistent_backend_config` as a JSON object containing `storage_account_name`, `container_name`, `key` and `resource_group_name`; its `key` points to the persistent root's state blob.

| Root | Environment variable | Purpose |
|---|---|---|
| All | `TF_VAR_naming_suffix` | Globally unique lowercase alphanumeric naming suffix |
| All | `TF_VAR_location` | Fixed UK South location; default `uksouth` |
| Persistent | `TF_VAR_runner_vnet_id` | Existing external runner VNet reference |
| Persistent | `TF_VAR_spoke_cidr` | Spoke address space |
| Persistent | `TF_VAR_aks_subnet_cidr` | AKS subnet prefix |
| Persistent | `TF_VAR_private_endpoint_subnet_cidr` | Private endpoint subnet prefix |
| Persistent | `TF_VAR_platform_dns_resource_group_name` | Existing platform DNS resource group; default `rg-platform-privatedns` |
| Persistent | `TF_VAR_create_private_dns_zones` | Standalone zone ownership; default `false` uses existing platform zones |
| Persistent | `TF_VAR_workflow_principal_id` | External protected workflow identity for registry push |
| Cluster, ephemeral | `TF_VAR_persistent_backend_config` | Read-only location of the persistent output state |
| Cluster, ephemeral | `TF_VAR_persistent_backend_use_oidc` | `true` for workflows (default); `false` for Hari's authenticated CLI |
| Cluster | `TF_VAR_pod_cidr` | Overlay pod prefix |
| Cluster | `TF_VAR_service_cidr` | Kubernetes service prefix |
| Cluster | `TF_VAR_dns_service_ip` | Cluster DNS address |
| Cluster | `TF_VAR_aks_authorized_ip_ranges` | Explicit authorized API server ranges |
| Cluster | `TF_VAR_kubernetes_version` | Explicit available GA Kubernetes patch version at least `1.35.0` |
| Ephemeral | `TF_VAR_eventhub_namespace_name` | Namespace name excluding `azure` |
| Ephemeral | `TF_VAR_scenario_id` | Fixed A01 scenario; default `A01` |
| Ephemeral | `TF_VAR_run_id` | Scenario phase run identifier |
| Ephemeral | `TF_VAR_expires_at` | UTC expiry used by the later sweeper |

Authentication uses standard `ARM_CLIENT_ID`, `ARM_TENANT_ID`, `ARM_SUBSCRIPTION_ID` and, for the protected workflow, `ARM_USE_OIDC`. These are variable names only. Agent sessions hold no Azure credentials under [AGENTS.md](../../AGENTS.md#governance-approved-by-hari-agents-may-propose-changes-in-a-pull-request-which-only-hari-merges).

Supply private backend files through `PARCHWELL_PERSISTENT_BACKEND_CONFIG`, `PARCHWELL_CLUSTER_BACKEND_CONFIG` and `PARCHWELL_EPHEMERAL_BACKEND_CONFIG`. Use three distinct state keys: persistent, cluster and ephemeral. The cluster and ephemeral `persistent_backend_config` objects both read the persistent key. Each backend selects Entra authentication. Remote-state reads select OIDC when `persistent_backend_use_oidc` is true; Hari sets it false to use his configured non-OIDC authentication. Terraform's [pinned Azure backend](https://github.com/hashicorp/terraform/blob/v1.16.6/internal/backend/remote-state/azure/backend.go) supports this selector and defaults CLI authentication on; this is not evidence that an operator has logged in successfully.

No state account, backend container, backend private endpoint or migration is owned here, following [Hari's backend decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/112#issuecomment-6318488448). Private plan paths are `PARCHWELL_PERSISTENT_PLAN_PATH`, `PARCHWELL_CLUSTER_PLAN_PATH` and `PARCHWELL_EPHEMERAL_PLAN_PATH`.

The backend, its credentials, Terraform state, plan files and operational outputs stay private. Backend files ending in `.tfbackend` or `.tfbackend.json`, state and plan files are ignored. Do not commit supplied deployment settings or actual identifiers. `terraform output -json` includes sensitive values; output annotations do not redact that JSON, so keep it private too ([pinned Terraform JSON renderer](https://github.com/hashicorp/terraform/blob/v1.16.6/internal/command/views/output.go)).

## Prerequisites for a later deployment

The existing backend must provide Entra authentication, disabled shared keys, private connectivity and narrowly scoped data-plane access for its authorized operators. That is a deployment prerequisite, not an observation made by these roots. The platform DNS zones, runner links and external deploying grants must already exist as [ADR-010](../blueprint.md#adr-010-platform-prerequisites-live-outside-this-repos-terraform) defines.

Before a credentialed plan, review real address spaces for overlap and API server authorized ranges for the intended operators. Select an available GA UK South Kubernetes patch at least `1.35.0`, as the [C1 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/157#issuecomment-6385941149) requires. Microsoft's [version calendar](https://github.com/MicrosoftDocs/azure-aks-docs/blob/d7becc7f8c5994904289fc298253a1ff302d0bdd/articles/aks/supported-kubernetes-versions.md) lists November 2026 as the end of support for 1.34. Input validation and mocks do not establish regional patch availability.

The [factory entry and smoke rules](../agents/factory.md#item-lifecycle) own the #138 sequence. [Hari's cleanup decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/157#issuecomment-6386075427) records that no dispatch or ephemeral environment remains active before C1 implementation. There is no state migration. Persistent run storage is deleted without an archive under the [parent entry decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6384675649); the external Terraform backend remains.

After the protected pull request merges, Hari reviews and applies the persistent changes and refreshes the private persistent-output input. That apply deletes the old persistent-state-owned AKS subnet role assignment. The cluster workflow then creates the same subnet-scoped Network Contributor grant before creating AKS. The identities themselves remain persistent.

Do not run the old Runner-based workflows against the new outputs. C2 owns the compatible smoke and sweeper cutover and recovery path before the first new environment. Parent gate 1 verifies cluster and ephemeral deployment; its ownership and bounds are in the [parent plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383206899). Passing offline checks supplies no live deployment, permission, ingestion, retention or cluster restart evidence.

## Operator commands

The following persistent sequence is run by Hari only, under the [owner execution rule](../engineering/terraform.md#checks-ci-required). Private file paths are supplied by the operator.

```sh
terraform -chdir=infra/persistent init -backend-config="$PARCHWELL_PERSISTENT_BACKEND_CONFIG"
terraform -chdir=infra/persistent plan -out="$PARCHWELL_PERSISTENT_PLAN_PATH"
terraform -chdir=infra/persistent apply "$PARCHWELL_PERSISTENT_PLAN_PATH"
```

The following cluster sequence belongs to the protected scenario or smoke workflow. Its start/reuse/stop behavior follows ADR-020 and C2's lifecycle implementation; the workflow never destroys this root.

```sh
terraform -chdir=infra/cluster init -backend-config="$PARCHWELL_CLUSTER_BACKEND_CONFIG"
terraform -chdir=infra/cluster plan -out="$PARCHWELL_CLUSTER_PLAN_PATH"
terraform -chdir=infra/cluster apply "$PARCHWELL_CLUSTER_PLAN_PATH"
```

The following ephemeral sequence belongs to the protected scenario or smoke workflow, or Hari by hand, under the same owner rule. It is documentation for that workflow, not an agent-session command.

```sh
terraform -chdir=infra/ephemeral init -backend-config="$PARCHWELL_EPHEMERAL_BACKEND_CONFIG"
terraform -chdir=infra/ephemeral plan -out="$PARCHWELL_EPHEMERAL_PLAN_PATH"
terraform -chdir=infra/ephemeral apply "$PARCHWELL_EPHEMERAL_PLAN_PATH"
```

Teardown and the sweeper follow [AGENTS.md hard safety rules](../../AGENTS.md#hard-safety-rules) and the [factory run lifecycle](../agents/factory.md#item-lifecycle). This change does not execute a live plan, apply, migration or destroy.

The ephemeral AzureRM provider sets `resource_group.prevent_deletion_if_contains_resources = false`. The [AzureRM 4.81.0 features guide](https://github.com/hashicorp/terraform-provider-azurerm/blob/v4.81.0/website/docs/guides/features-block.html.markdown#resource_group), read 2026-10-23, states that the default `true` requires all resources in a group to be deleted before the provider deletes the group. This change disables that preliminary check for the disposable ephemeral group, including resources outside Terraform state. Hari's teardown report in [#140](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/140) records an auto-created Application Insights smart detector alert rule that blocked group deletion. The issue directs this flag unless managing that auto-created rule has been verified; no such alternative has been verified for this repair. Persistent and cluster providers retain deletion protection. Cleanup follows ADR-020 and the compatible C2 workflow cutover; the new roots do not retain the Runner generation guard.

## Output contract

These output values are deployment data held in private state. They are not public experiment evidence.

| Root | Output | Fields or meaning |
|---|---|---|
| Persistent | `spoke_vnet_id` | Created spoke VNet |
| Persistent | `aks_subnet_id` | AKS subnet |
| Persistent | `private_endpoint_subnet_id` | Endpoint subnet |
| Persistent | `private_dns_zone_ids` | `eventhubs`, `blob` |
| Persistent | `log_analytics_workspace_id` | Persistent workspace |
| Persistent | `workload_identities` | `loadgen`, `enrichment-consumer`, `verifier`, `probe`, each with `id`, `client_id`, `principal_id` |
| Persistent | `aks_identity_id` | Separate AKS control-plane identity |
| Persistent | `acr` | `id`, `name`, `login_server` |
| Cluster | `aks` | `id`, `name`, `node_pools` |
| Ephemeral | `eventhubs` | `namespace_name`, `bootstrap_servers`, `topics` keyed `events`, `state`, `dlq` |
| Ephemeral | `blob` | `account_url`, `containers` keyed `ledger`, `verifier`, `claim_check`, `single_action` |
| Ephemeral | `private_endpoint_ips` | `eventhubs`, `blob` |
| Cluster | `workload_identity` | `namespace`, `service_accounts`, each with `name`, `client_id`, `subject` |
| Persistent | `application_insights_connection_string` | Sensitive runtime value |

The [adopted probe batch decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/212#issuecomment-6423299188) adds the probe identity and federation, its component-scoped telemetry grant, and exactly three data grants: probe receives Azure Event Hubs Data Receiver on `document-events` and Storage Blob Data Contributor on `single-action`; verifier receives Storage Blob Data Reader on `single-action`. Existing grants remain.

Before the S1 dispatch, Hari applies the persistent changes and refreshes PARCHWELL_PERSISTENT_OUTPUTS with the new probe identity. The workflow applies cluster federation and ephemeral roles. The image smoke is not an RBAC access test.

## Worker consumer interface

The namespace is `parchwell`. Service accounts are `loadgen`, `enrichment-consumer`, `verifier` and `probe`; each federation subject is `system:serviceaccount:parchwell:<service-account-name>`. Workers use the matching client IDs from the cluster root's `workload_identity` output.

The fixed pools are `system`, `workload` and `loadgen`. Each has one normal-capacity `Standard_D4ds_v5` node with autoscaling disabled for A01. The system taint is `CriticalAddonsOnly=true:NoSchedule`; the loadgen taint is `workload=loadgen:NoSchedule`. Pool labels use `workload=system`, `workload=workload` and `workload=loadgen`. These placement settings and resource sizes are design inputs from the approved plan, not measured capacity.

Kafka bootstrap uses the namespace hostname on port `9093`. Topic names are `document-events`, `document-state` and `document-events-dlq`; Blob containers are `ledger`, `verifier`, `claim-check` and `single-action`. The compacted topic's tombstone retention is a design choice of `168` hours from the approved plan, not a measured safe replay interval. The scoped identity contract is defined in [ADR-003](../blueprint.md#adr-003-entra-only-authentication-with-aks-workload-identity-and-a-custom-oauthbearer-refresh-handler) and [ADR-012](../blueprint.md#adr-012-the-oracle-is-a-per-message-ledger-joined-into-four-outcome-classes).

## Monitoring and retention

The persistent Application Insights component disables local authentication. Exactly the loadgen, verifier and probe identities receive Monitoring Metrics Publisher at that component's scope; enrichment receives no telemetry grant. Microsoft's [Entra authentication documentation](https://github.com/MicrosoftDocs/azure-monitor-docs/blob/91be653d05f39bed772fc55c09e2a0b2bcbd5f3b/articles/azure-monitor/app/azure-ad-authentication.md) describes that role's all-telemetry ingestion permission. The [pinned component module](https://github.com/Azure/terraform-azurerm-avm-res-insights-component/tree/848161e9cb89f4ef5a73d363e984e8dcdad0151d) supplies the resource and component-scoped grants; worker exporters arrive in CT.

The cluster's OMS addon uses Entra authentication and the persistent workspace. The separate Container Insights data collection rule (DCR) uses `Microsoft-ContainerInsights-Group-Default`, a one-minute interval, all namespaces and ContainerLogV2, following the defaults in Microsoft's [collection settings table](https://github.com/MicrosoftDocs/azure-monitor-docs/blob/91be653d05f39bed772fc55c09e2a0b2bcbd5f3b/articles/azure-monitor/containers/kubernetes-monitoring-enable.md#L440). The [Microsoft onboarding template](https://github.com/microsoft/Docker-Provider/blob/46052686001947b2c3afd9f4c38b9c254706e26d/scripts/onboarding/aks/onboarding-msi-terraform/main.tf) shows the addon, DCR and association wiring. [ADR-011](../blueprint.md#adr-011-terraform-with-azure-verified-modules-where-they-cover-the-need) records the DCR AVM and AzureRM association fallback. Bundled AKS monitoring and alerts remain disabled because the bundle also creates deferred Managed Prometheus resources.

[Hari's retention decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/157#issuecomment-6386010001) keeps the workspace at 30 days and the component at its existing 90-day default, with no per-table overrides. Microsoft's [retention documentation](https://github.com/MicrosoftDocs/azure-monitor-docs/blob/91be653d05f39bed772fc55c09e2a0b2bcbd5f3b/articles/azure-monitor/logs/data-retention-configure.md) describes these table defaults:

- `AppAvailabilityResults`, `AppBrowserTimings`, `AppDependencies`, `AppExceptions`, `AppEvents`, `AppMetrics`, `AppPageViews`, `AppPerformanceCounters`, `AppRequests`, `AppSystemEvents` and `AppTraces`: 90 days.
- Container Insights tables inherit the workspace's 30-day default unless separately configured.
- `Usage` and `AzureActivity`: at least 90 days by default.

These are configured and documented defaults, not measured deployed retention or an exact purge deadline. The workspace setting does not make every table's retention 30 days.

## Offline checks

Use the tool versions selected by [.terraform-version](../../.terraform-version), [.tflint.hcl](../../.tflint.hcl) and each root's `versions.tf` and lockfile. TFLint is pinned to `0.64.0` in the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/112#issuecomment-6318201099). After installing the pinned binaries and lint plugin, run from the repository root:

```sh
terraform fmt -check -recursive infra
terraform fmt -check -recursive tests/terraform
terraform -chdir=infra/persistent init -backend=false -input=false
terraform -chdir=infra/cluster init -backend=false -input=false
terraform -chdir=infra/ephemeral init -backend=false -input=false
terraform -chdir=infra/persistent validate -no-color
terraform -chdir=infra/cluster validate -no-color
terraform -chdir=infra/ephemeral validate -no-color
tflint --init --config="$PWD/.tflint.hcl"
tflint --chdir=infra/persistent --config="$PWD/.tflint.hcl"
tflint --chdir=infra/cluster --config="$PWD/.tflint.hcl"
tflint --chdir=infra/ephemeral --config="$PWD/.tflint.hcl"
terraform -chdir=infra/persistent test -test-directory=tests -no-color
terraform -chdir=infra/cluster test -test-directory=tests -no-color
terraform -chdir=infra/ephemeral test -test-directory=tests -no-color
npm run check:docs
git diff --check
```

Each root's `tests` symlink points to its canonical files under `tests/terraform/`. The local `-test-directory=tests` path is required because the pinned Terraform rejects an out-of-root testing-directory argument. The native tests use plan mode with actual pinned AVM configuration and computed-field mock overrides. [Fixture documentation](../../tests/terraform/fixtures/README.md) describes their synthetic inputs and limits. Passing checks establishes local schema and configuration-contract coverage; it does not establish backend readiness, Azure permissions, connectivity, emitted telemetry or Event Hubs behavior.

# Terraform foundations

The persistent root creates the shared network, identities, registry and Log Analytics workspace. The ephemeral root creates the A01 cluster, Event Hubs, evidence storage and private endpoints. Both roots consume Hari's existing external state backend; offline checks use synthetic provider responses.

The [approved implementation plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/112#issuecomment-6318201099) defines this interface. [Terraform engineering standards](../engineering/terraform.md) own execution, tagging and backend rules; [the blueprint](../blueprint.md#adr-011-terraform-with-azure-verified-modules-where-they-cover-the-need) owns module selection and documented fallbacks.

## Private deployment inputs

Supply values through the protected deployment environment. The following table lists names and purposes, not deployment values. Set `TF_VAR_persistent_backend_config` as a JSON object containing `storage_account_name`, `container_name`, `key` and `resource_group_name`; its `key` points to the persistent root's state blob.

| Root | Environment variable | Purpose |
|---|---|---|
| Both | `TF_VAR_naming_suffix` | Globally unique lowercase alphanumeric naming suffix |
| Both | `TF_VAR_location` | Fixed UK South location; default `uksouth` |
| Persistent | `TF_VAR_runner_vnet_id` | Existing external runner VNet reference |
| Persistent | `TF_VAR_spoke_cidr` | Spoke address space |
| Persistent | `TF_VAR_aks_subnet_cidr` | AKS subnet prefix |
| Persistent | `TF_VAR_private_endpoint_subnet_cidr` | Private endpoint subnet prefix |
| Persistent | `TF_VAR_platform_dns_resource_group_name` | Existing platform DNS resource group; default `rg-platform-privatedns` |
| Persistent | `TF_VAR_create_private_dns_zones` | Standalone zone ownership; default `false` uses existing platform zones |
| Persistent | `TF_VAR_budget_contact_emails` | Actual-spend alert recipients |
| Persistent | `TF_VAR_budget_start_date` | First day of the budget month at midnight UTC |
| Persistent | `TF_VAR_workflow_principal_id` | External protected workflow identity for registry push |
| Ephemeral | `TF_VAR_persistent_backend_config` | Read-only location of the persistent output state |
| Ephemeral | `TF_VAR_pod_cidr` | Overlay pod prefix |
| Ephemeral | `TF_VAR_service_cidr` | Kubernetes service prefix |
| Ephemeral | `TF_VAR_dns_service_ip` | Cluster DNS address |
| Ephemeral | `TF_VAR_aks_authorized_ip_ranges` | Explicit authorized API server ranges |
| Ephemeral | `TF_VAR_kubernetes_version` | Explicit available GA Kubernetes patch version |
| Ephemeral | `TF_VAR_eventhub_namespace_name` | Namespace name excluding `azure` |
| Ephemeral | `TF_VAR_scenario_id` | Fixed A01 scenario; default `A01` |
| Ephemeral | `TF_VAR_run_id` | Scenario phase run identifier |
| Ephemeral | `TF_VAR_expires_at` | UTC expiry used by the later sweeper |

Authentication uses standard `ARM_CLIENT_ID`, `ARM_TENANT_ID`, `ARM_SUBSCRIPTION_ID` and, for the protected workflow, `ARM_USE_OIDC`. These are variable names only. Agent sessions hold no Azure credentials under [AGENTS.md](../../AGENTS.md#governance-approved-by-hari-agents-may-propose-changes-in-a-pull-request-which-only-hari-merges).

Supply the two root backend configurations as private files referenced by `PARCHWELL_PERSISTENT_BACKEND_CONFIG` and `PARCHWELL_EPHEMERAL_BACKEND_CONFIG`. Choose distinct blob keys so each root keeps independent state. The ephemeral `persistent_backend_config` object reads the persistent key. The checked-in backend configuration explicitly selects Entra authentication; workflow remote-state reads also select OIDC. No state account, backend container, backend private endpoint or migration is owned here, following [Hari's decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/112#issuecomment-6318488448).

The backend, its credentials, Terraform state, plan files and operational outputs stay private. Backend files ending in `.tfbackend` or `.tfbackend.json`, state and plan files are ignored. Do not commit supplied deployment settings or actual identifiers.

## Prerequisites for a later deployment

The existing backend must provide Entra authentication, disabled shared keys, private connectivity and narrowly scoped data-plane access for its authorized operators. That is a deployment prerequisite, not an observation made by these roots. The platform DNS zones, runner links and external deploying grants must already exist as [ADR-010](../blueprint.md#adr-010-platform-prerequisites-live-outside-this-repos-terraform) defines.

Before a credentialed plan, review the real address spaces for overlap and the API server authorized ranges for the intended operators. Verify that the subscription's budget is in GBP and that the supplied start month is valid. Resolve an available GA UK South Kubernetes patch explicitly. The root validates its version string format; mocks do not establish GA availability.

Ephemeral deployment requires the reviewed persistent plan and Hari's apply receipt. A missing prerequisite stops that later deployment, not offline implementation. The later scenario workflow and sweeper remain entry C of the [approved split](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/109#issuecomment-6313658165).

## Operator commands

The following persistent sequence is run by Hari only, under the [owner execution rule](../engineering/terraform.md#checks). Private file paths are supplied by the operator.

```sh
terraform -chdir=infra/persistent init -backend-config="$PARCHWELL_PERSISTENT_BACKEND_CONFIG"
terraform -chdir=infra/persistent plan -out="$PARCHWELL_PERSISTENT_PLAN_PATH"
terraform -chdir=infra/persistent apply "$PARCHWELL_PERSISTENT_PLAN_PATH"
```

The following ephemeral sequence belongs only to the protected scenario workflow, under the same owner rule. It is documentation for that workflow, not an agent-session command.

```sh
terraform -chdir=infra/ephemeral init -backend-config="$PARCHWELL_EPHEMERAL_BACKEND_CONFIG"
terraform -chdir=infra/ephemeral plan -out="$PARCHWELL_EPHEMERAL_PLAN_PATH"
terraform -chdir=infra/ephemeral apply "$PARCHWELL_EPHEMERAL_PLAN_PATH"
```

Teardown and the sweeper follow [AGENTS.md hard safety rules](../../AGENTS.md#hard-safety-rules) and the [factory run lifecycle](../agents/factory.md#item-lifecycle). This change does not execute a live plan, apply, migration or destroy.

## Output contract

These output values are deployment data held in private state. They are not public experiment evidence.

| Root | Output | Fields or meaning |
|---|---|---|
| Persistent | `spoke_vnet_id` | Created spoke VNet |
| Persistent | `aks_subnet_id` | AKS subnet |
| Persistent | `private_endpoint_subnet_id` | Endpoint subnet |
| Persistent | `private_dns_zone_ids` | `eventhubs`, `blob` |
| Persistent | `log_analytics_workspace_id` | Persistent workspace |
| Persistent | `workload_identities` | `loadgen`, `enrichment-consumer`, `verifier`, each with `id`, `client_id`, `principal_id` |
| Persistent | `aks_identity_id` | Separate AKS control-plane identity |
| Persistent | `acr` | `id`, `name`, `login_server` |
| Ephemeral | `aks` | `id`, `name`, `node_pools` |
| Ephemeral | `eventhubs` | `namespace_name`, `bootstrap_servers`, `topics` keyed `events`, `state`, `dlq` |
| Ephemeral | `blob` | `account_url`, `containers` keyed `ledger`, `verifier`, `claim_check` |
| Ephemeral | `private_endpoint_ips` | `eventhubs`, `blob` |
| Ephemeral | `workload_identity` | `namespace`, `service_accounts`, each with `name`, `client_id`, `subject` |
| Ephemeral | `application_insights_connection_string` | Sensitive runtime value |

## Entry C consumer interface

The namespace is `parchwell`. Service accounts are `loadgen`, `enrichment-consumer` and `verifier`; each federation subject is `system:serviceaccount:parchwell:<service-account-name>`. Entry C must use the matching client IDs from `workload_identity`.

The fixed pools are `system`, `workload` and `loadgen`. Each has one normal-capacity `Standard_D4ds_v5` node with autoscaling disabled for A01. The system taint is `CriticalAddonsOnly=true:NoSchedule`; the loadgen taint is `workload=loadgen:NoSchedule`. Pool labels use `workload=system`, `workload=workload` and `workload=loadgen`. These placement settings and resource sizes are design inputs from the approved plan, not measured capacity.

Kafka bootstrap uses the namespace hostname on port `9093`. Topic names are `document-events`, `document-state` and `document-events-dlq`; Blob containers are `ledger`, `verifier` and `claim-check`. The compacted topic's tombstone retention is a design choice of `168` hours from the approved plan, not a measured safe replay interval. The scoped identity contract is defined in [ADR-003](../blueprint.md#adr-003-entra-only-authentication-with-aks-workload-identity-and-a-custom-oauthbearer-refresh-handler) and [ADR-012](../blueprint.md#adr-012-the-oracle-is-a-per-message-ledger-joined-into-four-outcome-classes).

## Offline checks

Use the tool versions selected by [.terraform-version](../../.terraform-version), [.tflint.hcl](../../.tflint.hcl) and each root's `versions.tf` and lockfile. TFLint is pinned to `0.64.0` in the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/112#issuecomment-6318201099). After installing the pinned binaries and lint plugin, run from the repository root:

```sh
terraform fmt -check -recursive infra
terraform fmt -check -recursive tests/terraform
terraform -chdir=infra/persistent init -backend=false -input=false
terraform -chdir=infra/ephemeral init -backend=false -input=false
terraform -chdir=infra/persistent validate -no-color
terraform -chdir=infra/ephemeral validate -no-color
tflint --init --config="$PWD/.tflint.hcl"
tflint --chdir=infra/persistent --config="$PWD/.tflint.hcl"
tflint --chdir=infra/ephemeral --config="$PWD/.tflint.hcl"
terraform -chdir=infra/persistent test -test-directory=tests -no-color
terraform -chdir=infra/ephemeral test -test-directory=tests -no-color
npm run check:docs
git diff --check
```

Each root's `tests` symlink points to its canonical files under `tests/terraform/`. The local `-test-directory=tests` path is required because the pinned Terraform rejects an out-of-root testing-directory argument. The native tests use plan mode with actual pinned AVM configuration and computed-field mock overrides. [Fixture documentation](../../tests/terraform/fixtures/README.md) describes their synthetic inputs and limits. Passing checks establishes local schema and configuration-contract coverage; it does not establish backend readiness, Azure permissions, connectivity, emitted telemetry or Event Hubs behavior.

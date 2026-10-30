# Terraform engineering standards

Binding for all Terraform in this repository. Status: approved by Hari on 2026-10-12.

## Layout

```
infra/
  persistent/     applied by Hari only: spoke VNet and peering, DNS links,
                  Log Analytics, Application Insights, identities and ACR
  cluster/        applied by workflows: reusable AKS, cluster federation and role assignments;
                  its own resource group and external backend key
  ephemeral/      applied/destroyed by repository workflows or Hari: Event Hubs,
                  run storage, claim-check storage and private endpoints
  scripts/        portable infrastructure lifecycle and phase scripts
  modules/        our own modules, only where no Azure Verified Module fits
tests/terraform/  terraform test files
```

Lifecycle ownership and cleanup are defined by [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates) and [AGENTS.md hard safety rules 1, 2 and 6](../../AGENTS.md#hard-safety-rules). This is the [#138 target layout](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383206899); C1 creates the third root.

Each root has `versions.tf` (required providers, exact versions), `variables.tf`, `outputs.tf`, `locals.tf`, and resource files grouped by concern (`network.tf`, `eventhubs.tf`, `identity.tf`). No resource definitions in `main.tf` beyond module calls.

## Modules and providers (ADR-011)

1. Azure Verified Modules where they expose what the design needs, pinned to an exact version.
2. `azurerm` resources where the module does not.
3. `azapi` only where `azurerm` also lacks the property.
Every fallback is recorded in ADR-011's table with the reason.

## Rules

- Provider and module versions pinned exactly. Upgrades in their own pull requests.
- Every variable has a `type`, a `description`, and a `validation` block where values are constrained (SKU names, CIDRs, PU counts).
- No hard-coded subscription IDs, tenant IDs, resource IDs or IP ranges. They come from variables or data sources.
- Names come from one `locals` naming function, not string literals scattered across files.
- Declare tags once in each root's `locals.tf` and pass that map to every resource and module that supports tags: `repo`, `layer`, `scenario`, `run-id`, `expires-at`. Do not configure provider default tags; the AzureRM provider does not expose them.
- The cluster and ephemeral layers read the persistent layer only through `terraform_remote_state` outputs or data sources, never by guessing names.
- No `count` or `for_each` keyed on values unknown until apply.

## Checks (CI, required)

- `terraform fmt -check -recursive`
- `terraform validate` per root
- `tflint` with the azurerm ruleset
- `terraform test` for modules and for variable validation
Cluster and ephemeral Azure-backed plans run only in the repository's protected scenario or smoke workflows immediately before apply, using environment-bound OIDC identity. Persistent plans and applies are run by Hari. Pull-request checks run offline and hold no Azure identity. This records [the adopted plan-location decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/109#issuecomment-6313658165).

All three roots use Hari's existing external Entra-authenticated private backend. Each root has its own backend key; the cluster has no state migration from an active ephemeral environment under [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates). This repository does not provision, import, modify or destroy that account or its networking. Backend permissions and connectivity are deployment prerequisites, not conclusions from mock tests. The external backend and shared tag map follow [Hari's decision on #112](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/112#issuecomment-6318488448); the unsupported provider field was checked against the [pinned AzureRM provider schema](https://github.com/hashicorp/terraform-provider-azurerm/blob/5782a75422c68a0d0804ac16d97dcaf3df5ee2fa/internal/provider/provider.go).

## What reviewers should not flag

Formatting that `terraform fmt` enforces. Module choices already recorded in ADR-011.

[ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates) removes persistent run storage and the budget alert. The external Terraform backend is retained and is not run-evidence storage. Mock plans establish configuration only.

## Scientific permission-audit reader prerequisites

The existing workflow OIDC principal needs Microsoft Graph application `Application.Read.All` with admin consent for unfiltered service-principal transitive memberships, `Microsoft.Authorization/roleAssignments/read` at the queried namespace and `Microsoft.Authorization/roleDefinitions/read` at every addressed definition scope. Sources: [Graph membership permissions](https://learn.microsoft.com/en-us/graph/api/serviceprincipal-list-transitivememberof?view=graph-rest-1.0), [assignment query](https://learn.microsoft.com/en-us/rest/api/authorization/role-assignments/list-for-scope?view=rest-authorization-2022-04-01), [definition query](https://learn.microsoft.com/en-us/rest/api/authorization/role-definitions/get-by-id?view=rest-authorization-2022-04-01) and [authorization operations](https://learn.microsoft.com/en-us/azure/role-based-access-control/permissions/management-and-governance#microsoftauthorization), read 2026-10-30. No reader or Graph consent is provisioned by this implementation; actual external availability is unknown.

Missing reads block scientific permission-scope clients. No workflow data grant, consent, secret replacement or broader identity change is implicit. The [single-action contract](../operations/single-action-contract.md#measured-permission-inventory-v8) owns the exact inventory, bounds and admission checks; [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates) owns the scoped Ubuntu-only production exception.

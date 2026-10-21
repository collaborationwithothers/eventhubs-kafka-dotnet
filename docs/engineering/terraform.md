# Terraform engineering standards

Binding for all Terraform in this repository. Status: approved by Hari on 2026-10-12.

## Layout

```
infra/
  persistent/     applied by Hari only: budget alert, spoke VNet and peering, DNS links,
                  Log Analytics, identities, ACR; consumes an external state backend
  ephemeral/      applied and destroyed by the scenario workflow only: Event Hubs, AKS,
                  storage, Application Insights, private endpoints
  modules/        our own modules, only where no Azure Verified Module fits
tests/terraform/  terraform test files
```

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
- The ephemeral layer reads the persistent layer only through `terraform_remote_state` outputs or data sources, never by guessing names.
- No `count` or `for_each` keyed on values unknown until apply.

## Checks (CI, required)

- `terraform fmt -check -recursive`
- `terraform validate` per root
- `tflint` with the azurerm ruleset
- `terraform test` for modules and for variable validation
Ephemeral Azure-backed plans run only in the protected scenario workflow immediately before apply, using its environment-bound OIDC identity. Persistent plans and applies are run by Hari. Pull-request checks run offline and hold no Azure identity. This records [the adopted plan-location decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/109#issuecomment-6313658165).

Both roots use Hari's existing external Entra-authenticated private backend. This repository does not provision, import, modify or destroy that account or its networking. Backend permissions and connectivity are deployment prerequisites, not conclusions from mock tests. The external backend and shared tag map follow [Hari's decision on #112](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/112#issuecomment-6318488448); the unsupported provider field was checked against the [pinned AzureRM provider schema](https://github.com/hashicorp/terraform-provider-azurerm/blob/5782a75422c68a0d0804ac16d97dcaf3df5ee2fa/internal/provider/provider.go).

## What reviewers should not flag

Formatting that `terraform fmt` enforces. Module choices already recorded in ADR-011.

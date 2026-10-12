# Terraform engineering standards

Binding for all Terraform in this repository. Status: approved by Hari on 2026-10-12.

## Layout

```
infra/
  persistent/     applied by Hari only: budget alert, spoke VNet and peering, DNS links,
                  Log Analytics, identities, state storage
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
- Tags applied once through the provider's default tags: `repo`, `layer`, `scenario`, `run-id`, `expires-at` (the sweeper reads `expires-at`).
- The ephemeral layer reads the persistent layer only through `terraform_remote_state` outputs or data sources, never by guessing names.
- No `count` or `for_each` keyed on values unknown until apply.

## Checks (CI, required)

- `terraform fmt -check -recursive`
- `terraform validate` per root
- `tflint` with the azurerm ruleset
- `terraform test` for modules and for variable validation
- `terraform plan` needs Azure credentials, so it cannot run on pull request events in a public repository (AGENTS.md safety rule 9). Open decision for Hari: whether plan runs inside the scenario workflow before apply only, or also from a separate workflow on branches in this repository with an identity Hari grants.

## What reviewers should not flag

Formatting that `terraform fmt` enforces. Module choices already recorded in ADR-011.

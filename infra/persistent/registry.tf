module "acr" {
  source                        = "Azure/avm-res-containerregistry-registry/azurerm"
  version                       = "0.8.0"
  name                          = local.acr_config.name
  location                      = local.acr_config.location
  resource_group_name           = local.acr_config.resource_group_name
  tags                          = local.acr_config.tags
  enable_telemetry              = local.acr_config.enable_telemetry
  sku                           = local.acr_config.sku
  public_network_access_enabled = local.acr_config.public_network_access_enabled
  admin_enabled                 = local.acr_config.admin_enabled
  anonymous_pull_enabled        = local.acr_config.anonymous_pull_enabled
  network_rule_set              = local.acr_config.network_rule_set
  zone_redundancy_enabled       = local.acr_config.zone_redundancy_enabled
  role_assignment_mode          = local.acr_config.role_assignment_mode
  private_endpoints             = local.acr_config.private_endpoints
}

resource "azurerm_role_assignment" "acr_push" {
  scope                = module.acr.resource_id
  role_definition_name = "AcrPush"
  principal_id         = var.workflow_principal_id
}

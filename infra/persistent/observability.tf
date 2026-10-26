module "log_analytics" {
  source                                               = "Azure/avm-res-operationalinsights-workspace/azurerm"
  version                                              = "0.5.1"
  name                                                 = local.names.log_analytics
  location                                             = var.location
  resource_group_name                                  = azurerm_resource_group.persistent.name
  tags                                                 = local.tags
  enable_telemetry                                     = false
  log_analytics_workspace_sku                          = "PerGB2018"
  log_analytics_workspace_retention_in_days            = 30
  log_analytics_workspace_local_authentication_enabled = false
  log_analytics_workspace_internet_ingestion_enabled   = "true"
  log_analytics_workspace_internet_query_enabled       = "true"
}

locals {
  application_insights_roles = { for key in toset(["loadgen", "verifier", "probe"]) : key => {
    role_definition_id_or_name       = "Monitoring Metrics Publisher"
    principal_id                     = module.workload_identities[key].principal_id
    principal_type                   = "ServicePrincipal"
    skip_service_principal_aad_check = true
  } }
}

module "application_insights" {
  source                        = "Azure/avm-res-insights-component/azurerm"
  version                       = "0.4.0"
  name                          = local.names.application_insights
  location                      = var.location
  resource_group_name           = azurerm_resource_group.persistent.name
  tags                          = local.tags
  enable_telemetry              = false
  workspace_id                  = module.log_analytics.resource_id
  application_type              = "other"
  internet_ingestion_enabled    = true
  internet_query_enabled        = true
  local_authentication_disabled = true
  retention_in_days             = 90
  role_assignments              = local.application_insights_roles
}

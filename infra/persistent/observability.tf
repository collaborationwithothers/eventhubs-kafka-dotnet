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

module "application_insights" {
  source = "Azure/avm-res-insights-component/azurerm"
  version = "0.4.0"
  name = local.names.application_insights
  location = var.location
  resource_group_name = azurerm_resource_group.ephemeral.name
  tags = local.tags
  enable_telemetry = false
  workspace_id = local.persistent.log_analytics_workspace_id
  application_type = "other"
  internet_ingestion_enabled = true
  internet_query_enabled = true
}

resource "azurerm_monitor_diagnostic_setting" "eventhubs" {
  name = local.names.diagnostic
  target_resource_id = module.eventhubs.resource_id
  log_analytics_workspace_id = local.persistent.log_analytics_workspace_id
  log_analytics_destination_type = "Dedicated"
  enabled_log {
    category_group = "allLogs"
  }
  enabled_metric {
    category = "AllMetrics"
  }
}

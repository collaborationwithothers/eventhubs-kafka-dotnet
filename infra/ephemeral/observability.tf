resource "azurerm_monitor_diagnostic_setting" "eventhubs" {
  count                          = var.eventhubs_enabled ? 1 : 0
  name                           = local.names.diagnostic
  target_resource_id             = module.eventhubs[0].resource_id
  log_analytics_workspace_id     = local.persistent.log_analytics_workspace_id
  log_analytics_destination_type = "Dedicated"
  enabled_log {
    category_group = "allLogs"
  }
  enabled_metric {
    category = "AllMetrics"
  }
}

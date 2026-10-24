module "container_insights" {
  source           = "Azure/avm-res-insights-datacollectionrule/azurerm"
  version          = "0.1.0"
  name             = local.names.container_insights
  parent_id        = azurerm_resource_group.cluster.id
  location         = var.location
  tags             = local.tags
  enable_telemetry = false
  kind             = "Linux"
  description      = "DCR for Azure Monitor Container Insights"

  data_sources = {
    extensions = [{
      name           = "ContainerInsightsExtension"
      extension_name = "ContainerInsights"
      streams        = ["Microsoft-ContainerInsights-Group-Default"]
      extension_settings = {
        dataCollectionSettings = {
          interval               = "1m"
          namespaceFilteringMode = "Off"
          namespaces             = []
          enableContainerLogV2   = true
        }
      }
    }]
  }
  destinations = {
    log_analytics = [{
      name                  = "ciworkspace"
      workspace_resource_id = local.persistent.log_analytics_workspace_id
    }]
  }
  data_flows = [{
    streams      = ["Microsoft-ContainerInsights-Group-Default"]
    destinations = ["ciworkspace"]
  }]
}

resource "azurerm_monitor_data_collection_rule_association" "container_insights" {
  name                    = "ContainerInsightsExtension"
  target_resource_id      = module.aks.resource_id
  data_collection_rule_id = module.container_insights.resource_id
  description             = "Container Insights collection for the reusable cluster."
}

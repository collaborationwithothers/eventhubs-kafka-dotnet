locals {
  eventhubs_config = {
    name                          = local.names.eventhub_namespace
    location                      = var.location
    resource_group_name           = azurerm_resource_group.ephemeral.name
    tags                          = local.tags
    enable_telemetry              = false
    sku                           = "Premium"
    capacity                      = 1
    auto_inflate_enabled          = false
    maximum_throughput_units      = null
    local_authentication_enabled  = false
    public_network_access_enabled = false
    event_hubs = {
      document-events     = { namespace_name = local.names.eventhub_namespace, resource_group_name = azurerm_resource_group.ephemeral.name, partition_count = 32, message_retention = 7, status = "Active", role_assignments = {} }
      document-events-dlq = { namespace_name = local.names.eventhub_namespace, resource_group_name = azurerm_resource_group.ephemeral.name, partition_count = 4, message_retention = 7, status = "Active", role_assignments = {} }
    }
    private_endpoints_manage_dns_zone_group = true
    private_endpoints = {
      eventhubs = {
        name                            = local.names.eventhubs_private_endpoint
        private_service_connection_name = "pse-${local.names.eventhub_namespace}"
        subnet_resource_id              = local.persistent.private_endpoint_subnet_id
        private_dns_zone_resource_ids   = [local.persistent.private_dns_zone_ids.eventhubs]
        tags                            = local.tags
      }
    }
  }
}

module "eventhubs" {
  source                                  = "Azure/avm-res-eventhub-namespace/azurerm"
  version                                 = "0.1.1"
  name                                    = local.eventhubs_config.name
  location                                = local.eventhubs_config.location
  resource_group_name                     = local.eventhubs_config.resource_group_name
  tags                                    = local.eventhubs_config.tags
  enable_telemetry                        = local.eventhubs_config.enable_telemetry
  sku                                     = local.eventhubs_config.sku
  capacity                                = local.eventhubs_config.capacity
  auto_inflate_enabled                    = local.eventhubs_config.auto_inflate_enabled
  maximum_throughput_units                = local.eventhubs_config.maximum_throughput_units
  local_authentication_enabled            = local.eventhubs_config.local_authentication_enabled
  public_network_access_enabled           = local.eventhubs_config.public_network_access_enabled
  event_hubs                              = local.eventhubs_config.event_hubs
  private_endpoints_manage_dns_zone_group = local.eventhubs_config.private_endpoints_manage_dns_zone_group
  private_endpoints                       = local.eventhubs_config.private_endpoints
}

# ADR-011: the pinned AVM cannot express the Compact cleanup policy.
# AGENTS.md hard safety rule 6 requires every ephemeral run to destroy its resources.
# tflint-ignore: azurerm_resources_missing_prevent_destroy
resource "azurerm_eventhub" "state" {
  name            = "document-state"
  namespace_id    = module.eventhubs.resource_id
  partition_count = 32
  retention_description {
    cleanup_policy                    = "Compact"
    tombstone_retention_time_in_hours = 168
  }
}

locals {
  workloads = toset(["loadgen", "enrichment-consumer", "verifier", "probe"])
  names = {
    application_insights    = "appi-parchwell-${var.naming_suffix}"
    resource_group          = "rg-parchwell-persist"
    spoke                   = "vnet-parchwell-${var.naming_suffix}"
    aks_subnet              = "snet-aks"
    private_endpoint_subnet = "snet-private-endpoints"
    spoke_to_runner         = "peer-parchwell-to-runner"
    runner_to_spoke         = "peer-runner-to-parchwell-${var.naming_suffix}"
    spoke_dns_link          = "link-parchwell-${var.naming_suffix}"
    runner_dns_link         = "link-runner-${var.naming_suffix}"
    log_analytics           = "law-parchwell-${var.naming_suffix}"
    acr                     = "acrparchwell${var.naming_suffix}"
    aks_identity            = "uami-aks-${var.naming_suffix}"
    workload_identities     = { for key in local.workloads : key => "uami-${key}-${var.naming_suffix}" }
  }
  tags = {
    repo         = "eventhubs-kafka-dotnet"
    layer        = "persistent"
    scenario     = "shared"
    "run-id"     = "persistent"
    "expires-at" = "never"
  }
  dns_names = {
    eventhubs = "privatelink.servicebus.windows.net"
    blob      = "privatelink.blob.core.windows.net"
  }
  private_dns_zone_ids = { for key, domain in local.dns_names : key => var.create_private_dns_zones ? module.owned_dns[key].resource_id : data.azurerm_private_dns_zone.platform[key].id }
  spoke_config = {
    name             = local.names.spoke
    location         = var.location
    parent_id        = azurerm_resource_group.persistent.id
    address_space    = toset([var.spoke_cidr])
    enable_telemetry = false
    tags             = local.tags
    subnets = {
      aks = { name = local.names.aks_subnet, address_prefix = var.aks_subnet_cidr }
      private_endpoints = {
        name                              = local.names.private_endpoint_subnet
        address_prefix                    = var.private_endpoint_subnet_cidr
        private_endpoint_network_policies = "Disabled"
      }
    }
    peerings = {
      runner = {
        name                               = local.names.spoke_to_runner
        remote_virtual_network_resource_id = var.runner_vnet_id
        create_reverse_peering             = true
        reverse_name                       = local.names.runner_to_spoke
      }
    }
  }
  dns_link_config = {
    spoke = { for key, id in local.private_dns_zone_ids : key => {
      name                                   = local.names.spoke_dns_link
      parent_id                              = id
      virtual_network_id                     = module.spoke.resource_id
      registration_enabled                   = false
      private_dns_zone_supports_private_link = true
      resolution_policy                      = "Default"
      tags                                   = local.tags
    } }
    runner = { for key, id in local.private_dns_zone_ids : key => {
      name                                   = local.names.runner_dns_link
      parent_id                              = id
      virtual_network_id                     = var.runner_vnet_id
      registration_enabled                   = false
      private_dns_zone_supports_private_link = true
      resolution_policy                      = "Default"
      tags                                   = local.tags
    } if var.create_private_dns_zones }
  }
  acr_config = {
    name                          = local.names.acr
    location                      = var.location
    resource_group_name           = azurerm_resource_group.persistent.name
    tags                          = local.tags
    enable_telemetry              = false
    sku                           = "Standard"
    public_network_access_enabled = true
    admin_enabled                 = false
    anonymous_pull_enabled        = false
    network_rule_set              = null
    zone_redundancy_enabled       = false
    role_assignment_mode          = "LegacyRegistryPermissions"
    private_endpoints             = {}
  }
}

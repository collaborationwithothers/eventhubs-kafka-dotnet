module "spoke" {
  source           = "Azure/avm-res-network-virtualnetwork/azurerm"
  version          = "0.22.2"
  name             = local.spoke_config.name
  location         = local.spoke_config.location
  parent_id        = local.spoke_config.parent_id
  address_space    = local.spoke_config.address_space
  enable_telemetry = local.spoke_config.enable_telemetry
  tags             = local.spoke_config.tags
  subnets          = local.spoke_config.subnets
  peerings         = local.spoke_config.peerings
}

data "azurerm_private_dns_zone" "platform" {
  for_each            = var.create_private_dns_zones ? {} : local.dns_names
  name                = each.value
  resource_group_name = var.platform_dns_resource_group_name
}

module "owned_dns" {
  for_each         = var.create_private_dns_zones ? local.dns_names : {}
  source           = "Azure/avm-res-network-privatednszone/azurerm"
  version          = "0.5.0"
  domain_name      = each.value
  parent_id        = azurerm_resource_group.persistent.id
  enable_telemetry = false
  tags             = local.tags
}

module "spoke_dns_links" {
  for_each                               = local.dns_link_config.spoke
  source                                 = "Azure/avm-res-network-privatednszone/azurerm//modules/private_dns_virtual_network_link"
  version                                = "0.5.0"
  name                                   = each.value.name
  parent_id                              = each.value.parent_id
  virtual_network_id                     = each.value.virtual_network_id
  registration_enabled                   = each.value.registration_enabled
  private_dns_zone_supports_private_link = each.value.private_dns_zone_supports_private_link
  resolution_policy                      = each.value.resolution_policy
  tags                                   = each.value.tags
}

module "runner_dns_links" {
  for_each                               = local.dns_link_config.runner
  source                                 = "Azure/avm-res-network-privatednszone/azurerm//modules/private_dns_virtual_network_link"
  version                                = "0.5.0"
  name                                   = each.value.name
  parent_id                              = each.value.parent_id
  virtual_network_id                     = each.value.virtual_network_id
  registration_enabled                   = each.value.registration_enabled
  private_dns_zone_supports_private_link = each.value.private_dns_zone_supports_private_link
  resolution_policy                      = each.value.resolution_policy
  tags                                   = each.value.tags
}

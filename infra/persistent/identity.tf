module "workload_identities" {
  for_each                       = local.workloads
  source                         = "Azure/avm-res-managedidentity-userassignedidentity/azurerm"
  version                        = "0.5.3"
  name                           = local.names.workload_identities[each.key]
  location                       = var.location
  resource_group_name            = azurerm_resource_group.persistent.name
  enable_telemetry               = false
  tags                           = local.tags
  federated_identity_credentials = {}
  role_assignments               = {}
}

module "aks_identity" {
  source                         = "Azure/avm-res-managedidentity-userassignedidentity/azurerm"
  version                        = "0.5.3"
  name                           = local.names.aks_identity
  location                       = var.location
  resource_group_name            = azurerm_resource_group.persistent.name
  enable_telemetry               = false
  tags                           = local.tags
  federated_identity_credentials = {}
  role_assignments               = {}
}

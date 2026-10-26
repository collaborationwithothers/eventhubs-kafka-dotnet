locals {
  storage_config = {
    name                            = local.names.storage
    location                        = var.location
    parent_id                       = azurerm_resource_group.ephemeral.id
    tags                            = local.tags
    enable_telemetry                = false
    account_kind                    = "StorageV2"
    account_sku_name                = "Standard_LRS"
    access_tier                     = "Hot"
    public_network_access_enabled   = false
    shared_access_key_enabled       = false
    default_to_oauth_authentication = true
    allow_nested_items_to_be_public = false
    https_traffic_only_enabled      = true
    min_tls_version                 = "TLS1_2"
    role_assignments                = {}
    containers = {
      ledger        = { name = "ledger", public_access = "None", role_assignments = {} }
      verifier      = { name = "verifier", public_access = "None", role_assignments = {} }
      claim_check   = { name = "claim-check", public_access = "None", role_assignments = {} }
      single_action = { name = "single-action", public_access = "None", role_assignments = {} }
    }
    private_endpoints_manage_dns_zone_group = true
    private_endpoints = {
      blob = {
        name                          = local.names.blob_private_endpoint
        subnet_resource_id            = local.persistent.private_endpoint_subnet_id
        subresource_name              = "blob"
        private_dns_zone_resource_ids = [local.persistent.private_dns_zone_ids.blob]
        tags                          = local.tags
        role_assignments              = {}
      }
    }
  }
}

module "storage" {
  source                                  = "Azure/avm-res-storage-storageaccount/azurerm"
  version                                 = "0.10.0"
  name                                    = local.storage_config.name
  location                                = local.storage_config.location
  parent_id                               = local.storage_config.parent_id
  tags                                    = local.storage_config.tags
  enable_telemetry                        = local.storage_config.enable_telemetry
  account_kind                            = local.storage_config.account_kind
  account_sku_name                        = local.storage_config.account_sku_name
  access_tier                             = local.storage_config.access_tier
  public_network_access_enabled           = local.storage_config.public_network_access_enabled
  shared_access_key_enabled               = local.storage_config.shared_access_key_enabled
  default_to_oauth_authentication         = local.storage_config.default_to_oauth_authentication
  allow_nested_items_to_be_public         = local.storage_config.allow_nested_items_to_be_public
  https_traffic_only_enabled              = local.storage_config.https_traffic_only_enabled
  min_tls_version                         = local.storage_config.min_tls_version
  role_assignments                        = local.storage_config.role_assignments
  containers                              = local.storage_config.containers
  private_endpoints_manage_dns_zone_group = local.storage_config.private_endpoints_manage_dns_zone_group
  private_endpoints                       = local.storage_config.private_endpoints
}

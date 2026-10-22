locals {
  run_storage_config = {
    name                            = local.names.run_storage
    location                        = var.location
    parent_id                       = azurerm_resource_group.persistent.id
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
      ledger   = { name = "ledger", public_access = "None", role_assignments = {} }
      evidence = { name = "evidence", public_access = "None", role_assignments = {} }
    }
    private_endpoints_manage_dns_zone_group = true
    private_endpoints = {
      blob = {
        name                          = local.names.run_blob_private_endpoint
        subnet_resource_id            = module.spoke.subnets.private_endpoints.resource_id
        subresource_name              = "blob"
        private_dns_zone_resource_ids = [local.private_dns_zone_ids.blob]
        tags                          = local.tags
        role_assignments              = {}
      }
    }
  }
}

module "run_storage" {
  source                                  = "Azure/avm-res-storage-storageaccount/azurerm"
  version                                 = "0.10.0"
  name                                    = local.run_storage_config.name
  location                                = local.run_storage_config.location
  parent_id                               = local.run_storage_config.parent_id
  tags                                    = local.run_storage_config.tags
  enable_telemetry                        = local.run_storage_config.enable_telemetry
  account_kind                            = local.run_storage_config.account_kind
  account_sku_name                        = local.run_storage_config.account_sku_name
  access_tier                             = local.run_storage_config.access_tier
  public_network_access_enabled           = local.run_storage_config.public_network_access_enabled
  shared_access_key_enabled               = local.run_storage_config.shared_access_key_enabled
  default_to_oauth_authentication         = local.run_storage_config.default_to_oauth_authentication
  allow_nested_items_to_be_public         = local.run_storage_config.allow_nested_items_to_be_public
  https_traffic_only_enabled              = local.run_storage_config.https_traffic_only_enabled
  min_tls_version                         = local.run_storage_config.min_tls_version
  role_assignments                        = local.run_storage_config.role_assignments
  containers                              = local.run_storage_config.containers
  private_endpoints_manage_dns_zone_group = local.run_storage_config.private_endpoints_manage_dns_zone_group
  private_endpoints                       = local.run_storage_config.private_endpoints
}

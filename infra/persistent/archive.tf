locals {
  archive_config = {
    name                            = local.names.archive_storage
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
      run_evidence = {
        name          = "run-evidence"
        public_access = "None"
        role_assignments = {
          workflow = {
            role_definition_id_or_name       = "Storage Blob Data Contributor"
            principal_id                     = var.workflow_principal_id
            principal_type                   = "ServicePrincipal"
            skip_service_principal_aad_check = true
          }
        }
      }
    }
    blob_properties = {
      versioning_enabled                = false
      delete_retention_policy           = { enabled = false }
      container_delete_retention_policy = { enabled = false }
    }
    storage_management_policy_rule = {
      delete_run_evidence = {
        name    = "delete-run-evidence-after-90-days"
        enabled = true
        filters = { blob_types = ["blockBlob"], prefix_match = ["run-evidence/"] }
        actions = { base_blob = { delete_after_days_since_creation_greater_than = 90 } }
      }
    }
    private_endpoints_manage_dns_zone_group = true
    private_endpoints = {
      blob = {
        name                          = local.names.archive_blob_private_endpoint
        subnet_resource_id            = module.spoke.subnets.private_endpoints.resource_id
        subresource_name              = "blob"
        private_dns_zone_resource_ids = [local.private_dns_zone_ids.blob]
        tags                          = local.tags
        role_assignments              = {}
      }
    }
  }
}

module "archive" {
  source                                  = "Azure/avm-res-storage-storageaccount/azurerm"
  version                                 = "0.10.0"
  name                                    = local.archive_config.name
  location                                = local.archive_config.location
  parent_id                               = local.archive_config.parent_id
  tags                                    = local.archive_config.tags
  enable_telemetry                        = local.archive_config.enable_telemetry
  account_kind                            = local.archive_config.account_kind
  account_sku_name                        = local.archive_config.account_sku_name
  access_tier                             = local.archive_config.access_tier
  public_network_access_enabled           = local.archive_config.public_network_access_enabled
  shared_access_key_enabled               = local.archive_config.shared_access_key_enabled
  default_to_oauth_authentication         = local.archive_config.default_to_oauth_authentication
  allow_nested_items_to_be_public         = local.archive_config.allow_nested_items_to_be_public
  https_traffic_only_enabled              = local.archive_config.https_traffic_only_enabled
  min_tls_version                         = local.archive_config.min_tls_version
  role_assignments                        = local.archive_config.role_assignments
  containers                              = local.archive_config.containers
  blob_properties                         = local.archive_config.blob_properties
  storage_management_policy_rule          = local.archive_config.storage_management_policy_rule
  private_endpoints_manage_dns_zone_group = local.archive_config.private_endpoints_manage_dns_zone_group
  private_endpoints                       = local.archive_config.private_endpoints
}

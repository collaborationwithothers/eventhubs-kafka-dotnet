data "terraform_remote_state" "persistent" {
  backend = "azurerm"
  config  = merge(var.persistent_backend_config, { use_azuread_auth = true, use_oidc = true })
}

# The storage AVM exposes endpoint identity but not its private IP address.
# Deriving the name from its computed ID waits for endpoint creation without
# deferring this lookup for changes to unrelated storage child resources.
data "azurerm_private_endpoint_connection" "blob" {
  name                = basename(module.storage.private_endpoints.blob.id)
  resource_group_name = local.names.resource_group
}

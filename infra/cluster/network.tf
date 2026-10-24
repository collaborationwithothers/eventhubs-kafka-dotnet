data "terraform_remote_state" "persistent" {
  backend = "azurerm"
  config  = merge(var.persistent_backend_config, { use_azuread_auth = true, use_oidc = var.persistent_backend_use_oidc })
}

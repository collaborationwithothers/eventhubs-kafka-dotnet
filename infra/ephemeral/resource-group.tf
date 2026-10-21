resource "azurerm_resource_group" "ephemeral" {
  name     = local.names.resource_group
  location = var.location
  tags     = local.tags
}

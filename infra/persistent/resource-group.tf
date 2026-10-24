resource "azurerm_resource_group" "persistent" {
  name     = local.names.resource_group
  location = var.location
  tags     = local.tags
}

resource "azurerm_resource_group" "cluster" {
  name     = local.names.resource_group
  location = var.location
  tags     = local.tags
}

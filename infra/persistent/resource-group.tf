resource "azurerm_resource_group" "persistent" {
  name       = local.names.resource_group
  location   = var.location
  tags       = local.tags
  depends_on = [azurerm_consumption_budget_subscription.monthly]
}

data "azurerm_subscription" "current" {}

resource "azurerm_consumption_budget_subscription" "monthly" {
  name            = local.names.budget
  subscription_id = data.azurerm_subscription.current.id
  amount          = 150
  time_grain      = "Monthly"
  time_period {
    start_date = var.budget_start_date
  }
  dynamic "notification" {
    for_each = toset(["50", "80", "100"])
    content {
      enabled        = true
      operator       = "GreaterThanOrEqualTo"
      threshold      = tonumber(notification.key)
      threshold_type = "Actual"
      contact_emails = var.budget_contact_emails
    }
  }
}

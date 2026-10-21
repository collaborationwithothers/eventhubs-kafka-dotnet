provider "azurerm" {
  features {}
  storage_use_azuread = true
}
provider "azapi" {}
provider "random" {}
provider "modtm" {}
provider "time" {}

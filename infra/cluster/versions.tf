terraform {
  required_version = "= 1.16.6"
  required_providers {
    azurerm = { source = "hashicorp/azurerm", version = "= 4.81.0" }
    azapi   = { source = "Azure/azapi", version = "= 2.13.0" }
    random  = { source = "hashicorp/random", version = "= 3.9.1" }
    modtm   = { source = "Azure/modtm", version = "= 0.3.5" }
    time    = { source = "hashicorp/time", version = "= 0.13.1" }
  }
}

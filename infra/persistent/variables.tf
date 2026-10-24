variable "naming_suffix" {
  type        = string
  description = "Eight-character suffix for generated Azure names."
  validation {
    condition     = can(regex("^[a-z0-9]{8}$", var.naming_suffix))
    error_message = "naming_suffix: invalid value."
  }
}

variable "location" {
  type        = string
  description = "Azure region for the persistent resources."
  default     = "uksouth"
  validation {
    condition     = var.location == "uksouth"
    error_message = "location: invalid value."
  }
}

variable "runner_vnet_id" {
  type        = string
  description = "Existing runner virtual network resource ID."
  validation {
    condition     = can(regex("(?i)^/subscriptions/[0-9a-f-]{36}/resourceGroups/[^/]+/providers/Microsoft.Network/virtualNetworks/[^/]+$", var.runner_vnet_id))
    error_message = "runner_vnet_id: invalid value."
  }
}

variable "spoke_cidr" {
  type        = string
  description = "IPv4 address space for the spoke virtual network."
  validation {
    condition     = can(cidrnetmask(var.spoke_cidr))
    error_message = "spoke_cidr: invalid value."
  }
}

variable "aks_subnet_cidr" {
  type        = string
  description = "IPv4 address prefix for the AKS subnet."
  validation {
    condition     = can(cidrnetmask(var.aks_subnet_cidr))
    error_message = "aks_subnet_cidr: invalid value."
  }
}

variable "private_endpoint_subnet_cidr" {
  type        = string
  description = "IPv4 address prefix for the private endpoint subnet."
  validation {
    condition     = can(cidrnetmask(var.private_endpoint_subnet_cidr))
    error_message = "private_endpoint_subnet_cidr: invalid value."
  }
}

variable "platform_dns_resource_group_name" {
  type        = string
  description = "Resource group containing the existing platform private DNS zones."
  default     = "rg-platform-privatedns"
  validation {
    condition     = length(var.platform_dns_resource_group_name) >= 1 && length(var.platform_dns_resource_group_name) <= 90
    error_message = "platform_dns_resource_group_name: invalid value."
  }
}

variable "create_private_dns_zones" {
  type        = bool
  description = "Create standalone private DNS zones and runner links instead of consuming platform zones."
  default     = false
}

variable "workflow_principal_id" {
  type        = string
  description = "Existing workflow principal receiving registry push permission."
  validation {
    condition     = can(regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", var.workflow_principal_id))
    error_message = "workflow_principal_id: invalid value."
  }
}

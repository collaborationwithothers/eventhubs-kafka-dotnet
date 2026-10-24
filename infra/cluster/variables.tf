variable "naming_suffix" {
  type        = string
  description = "Suffix shared by generated resource names."
  validation {
    condition     = can(regex("^[a-z0-9]{8}$", var.naming_suffix))
    error_message = "naming_suffix: invalid value."
  }
}

variable "location" {
  type        = string
  description = "Azure region for the cluster environment."
  default     = "uksouth"
  validation {
    condition     = var.location == "uksouth"
    error_message = "location: invalid value."
  }
}

variable "persistent_backend_config" {
  type        = object({ storage_account_name = string, container_name = string, key = string, resource_group_name = string })
  description = "Private location of the existing persistent state backend."
  validation {
    condition     = try(alltrue([for value in values(var.persistent_backend_config) : length(trimspace(value)) > 0]), false)
    error_message = "persistent_backend_config: invalid value."
  }
}

variable "pod_cidr" {
  type        = string
  description = "IPv4 address range for AKS overlay pods."
  validation {
    condition     = can(cidrnetmask(var.pod_cidr))
    error_message = "pod_cidr: invalid value."
  }
}

variable "service_cidr" {
  type        = string
  description = "IPv4 address range for Kubernetes services."
  validation {
    condition     = can(cidrnetmask(var.service_cidr))
    error_message = "service_cidr: invalid value."
  }
}

variable "dns_service_ip" {
  type        = string
  description = "Kubernetes DNS service address."
  validation {
    condition     = can(cidrhost("${var.dns_service_ip}/32", 0))
    error_message = "dns_service_ip: invalid value."
  }
}

variable "aks_authorized_ip_ranges" {
  type        = list(string)
  description = "IPv4 CIDRs allowed to reach the AKS API server."
  validation {
    condition     = try(length(var.aks_authorized_ip_ranges) > 0 && alltrue([for value in var.aks_authorized_ip_ranges : can(cidrnetmask(value))]), false)
    error_message = "aks_authorized_ip_ranges: invalid value."
  }
}

variable "kubernetes_version" {
  type        = string
  description = "Explicit Kubernetes patch version selected before deployment."
  validation {
    condition = try(
      can(regex("^[0-9]+[.][0-9]+[.][0-9]+$", var.kubernetes_version)) &&
      (tonumber(split(".", var.kubernetes_version)[0]) > 1 ||
        (tonumber(split(".", var.kubernetes_version)[0]) == 1 &&
      tonumber(split(".", var.kubernetes_version)[1]) >= 35)),
      false
    )
    error_message = "kubernetes_version: supply an explicit patch version at least 1.35.0."
  }
}

variable "persistent_backend_use_oidc" {
  type        = bool
  default     = true
  description = "Use workload OIDC for the persistent backend; false selects the operator's configured non-OIDC authentication."
}

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
  description = "Azure region for the ephemeral environment."
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

variable "eventhub_namespace_name" {
  type        = string
  description = "Globally unique Event Hubs namespace name."
  validation {
    condition     = try(can(regex("^[a-zA-Z][a-zA-Z0-9-]{4,48}[a-zA-Z0-9]$", var.eventhub_namespace_name)) && !strcontains(lower(var.eventhub_namespace_name), "azure"), false)
    error_message = "eventhub_namespace_name: invalid value."
  }
}

variable "scenario_id" {
  type        = string
  description = "Frozen inventory item using this environment."
  default     = "A01"
  validation {
    condition     = var.scenario_id == "A01"
    error_message = "scenario_id: invalid value."
  }
}

variable "run_id" {
  type        = string
  description = "Run identifier applied to environment tags."
  validation {
    condition     = can(regex("^[a-z0-9][a-z0-9-]{0,31}$", var.run_id))
    error_message = "run_id: invalid value."
  }
}

variable "expires_at" {
  type        = string
  description = "UTC expiry timestamp consumed by the sweeper."
  validation {
    condition     = try(can(formatdate("YYYY-MM-DD", var.expires_at)) && can(regex("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$", var.expires_at)), false)
    error_message = "expires_at: invalid value."
  }
}

variable "persistent_backend_use_oidc" {
  type        = bool
  default     = true
  description = "Use workload OIDC for the persistent backend; false selects the operator's configured non-OIDC authentication."
}

variable "probe_namespace_data_roles" {
  type        = bool
  default     = false
  description = "Enable the explicitly approved test-only probe V2 namespace Sender and Receiver exception."
}

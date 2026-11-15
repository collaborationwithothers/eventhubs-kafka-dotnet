output "spoke_vnet_id" {
  description = "Spoke virtual network resource ID."
  value       = module.spoke.resource_id
}

output "aks_subnet_id" {
  description = "AKS subnet resource ID."
  value       = module.spoke.subnets.aks.resource_id
}

output "private_endpoint_subnet_id" {
  description = "Private endpoint subnet resource ID."
  value       = module.spoke.subnets.private_endpoints.resource_id
}

output "private_dns_zone_ids" {
  description = "Private DNS zone IDs keyed by eventhubs and blob."
  value       = local.private_dns_zone_ids
}

output "log_analytics_workspace_id" {
  description = "Shared Log Analytics workspace resource ID."
  value       = module.log_analytics.resource_id
}

output "workload_identities" {
  description = "Workload identity resource, client and principal IDs."
  value       = { for key, identity in module.workload_identities : key => { id = identity.resource_id, client_id = identity.client_id, principal_id = identity.principal_id } }
}

output "aks_identity_id" {
  description = "AKS control-plane managed identity resource ID."
  value       = module.aks_identity.resource_id
}

output "acr" {
  description = "Shared registry resource ID, name and login server."
  value       = { id = module.acr.resource_id, name = module.acr.name, login_server = module.acr.login_server }
}

output "application_insights_connection_string" {
  description = "Application Insights exporter configuration for scenario deployment."
  sensitive   = true
  value       = module.application_insights.connection_string
}

output "evidence_archive" {
  description = "Private archive storage and retention configuration."
  value = {
    storage_account_id = module.archive.resource_id
    account_name       = local.names.archive_storage
    blob_service_uri   = "https://${module.archive.fqdn.blob}"
    container_name     = module.archive.containers.run_evidence.name
    container_id       = module.archive.containers.run_evidence.id
    retention_days     = 90
  }
}

output "workflow_principal_id" {
  description = "Existing workflow principal for optional smoke source access."
  sensitive   = true
  value       = var.workflow_principal_id
}

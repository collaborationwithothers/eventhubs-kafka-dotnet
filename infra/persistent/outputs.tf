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

output "run_storage" {
  description = "Persistent private reservation ledger and raw evidence account; grants remain external prerequisites."
  value = {
    id          = module.run_storage.resource_id
    account_url = "https://${module.run_storage.fqdn.blob}"
    containers = {
      ledger   = module.run_storage.containers["ledger"].name
      evidence = module.run_storage.containers["evidence"].name
    }
  }
}

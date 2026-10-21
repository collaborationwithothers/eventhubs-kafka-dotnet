output "aks" {
  description = "AKS identity and fixed pool names for scenario deployment."
  value       = { id = module.aks.resource_id, name = local.names.aks, node_pools = ["system", "workload", "loadgen"] }
}
output "eventhubs" {
  description = "Kafka bootstrap address and topic names for the workers."
  value = {
    namespace_name    = local.names.eventhub_namespace
    bootstrap_servers = "${local.names.eventhub_namespace}.servicebus.windows.net:9093"
    topics            = { events = "document-events", state = "document-state", dlq = "document-events-dlq" }
  }
}
output "blob" {
  description = "Blob account URL and the three workload container names."
  value = {
    account_url = "https://${module.storage.fqdn.blob}"
    containers  = { ledger = module.storage.containers.ledger.name, verifier = module.storage.containers.verifier.name, claim_check = module.storage.containers.claim_check.name }
  }
}
output "private_endpoint_ips" {
  description = "Private endpoint addresses for scenario connectivity probes."
  value = {
    eventhubs = module.eventhubs.private_endpoints.eventhubs.private_service_connection[0].private_ip_address
    blob      = data.azurerm_private_endpoint_connection.blob.private_service_connection[0].private_ip_address
  }
}
output "workload_identity" {
  description = "Namespace and service-account federation contract for entry C."
  value = {
    namespace = "parchwell"
    service_accounts = { for key in local.workloads : key => {
      name      = key
      client_id = local.persistent.workload_identities[key].client_id
      subject   = "system:serviceaccount:parchwell:${key}"
    } }
  }
}
output "application_insights_connection_string" {
  description = "Application Insights exporter configuration for scenario deployment."
  sensitive   = true
  value       = module.application_insights.connection_string
}

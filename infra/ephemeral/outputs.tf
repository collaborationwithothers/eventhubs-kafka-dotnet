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

output "eventhubs" {
  description = "Kafka bootstrap address and topic names for the workers."
  value = var.eventhubs_enabled ? {
    namespace_name    = local.names.eventhub_namespace
    bootstrap_servers = "${local.names.eventhub_namespace}.servicebus.windows.net:9093"
    topics            = { events = "document-events", state = "document-state", dlq = "document-events-dlq" }
  } : null
}
output "blob" {
  description = "Blob account URL and workload container names."
  value = {
    account_url = "https://${module.storage.fqdn.blob}"
    containers  = merge({ ledger = module.storage.containers.ledger.name, verifier = module.storage.containers.verifier.name, claim_check = module.storage.containers.claim_check.name, single_action = module.storage.containers.single_action.name }, var.archive_smoke_enabled ? { archive_smoke_source = module.storage.containers.archive_smoke_source.name } : {})
  }
}
output "private_endpoint_ips" {
  description = "Private endpoint addresses for scenario connectivity probes."
  value = {
    eventhubs = var.eventhubs_enabled ? module.eventhubs[0].private_endpoints.eventhubs.private_service_connection[0].private_ip_address : null
    blob      = data.azurerm_private_endpoint_connection.blob.private_service_connection[0].private_ip_address
  }
}

output "probe_v2_role_scope" {
  description = "Private applied identity and role-scope evidence for probe V2; not proof of absent inherited access."
  sensitive   = true
  value = var.eventhubs_enabled ? {
    namespace_id = module.eventhubs[0].resource_id
    topics = {
      document-events     = module.eventhubs[0].resource_eventhubs["document-events"].id
      document-state      = azurerm_eventhub.state[0].id
      document-events-dlq = module.eventhubs[0].resource_eventhubs["document-events-dlq"].id
    }
    accounts = {
      for account in ["loadgen", "probe", "enrichment-consumer", "verifier"] : account => {
        client_id    = local.persistent.workload_identities[account].client_id
        principal_id = local.persistent.workload_identities[account].principal_id
      }
    }
    assignments = {
      for key, role in local.workload_roles : key => {
        service_account      = role.workload
        role_definition_name = azurerm_role_assignment.workload[key].role_definition_name
        scope                = azurerm_role_assignment.workload[key].scope
        principal_id         = azurerm_role_assignment.workload[key].principal_id
      }
    }
    probe_namespace_data_roles = var.probe_namespace_data_roles
  } : null
}

output "configuration_lifecycle" {
  description = "Private resource identities for namespace lifecycle preservation checks."
  sensitive   = true
  value = {
    eventhubs_enabled                      = var.eventhubs_enabled
    storage_account_resource_id            = module.storage.resource_id
    blob_private_endpoint_resource_id      = module.storage.private_endpoints.blob.id
    container_resource_ids                 = { for key, container in module.storage.containers : key => container.id }
    workspace_resource_id                  = local.persistent.log_analytics_workspace_id
    blob_diagnostic_target_resource_id     = "${module.storage.resource_id}/blobServices/default"
    namespace_resource_id                  = var.eventhubs_enabled ? module.eventhubs[0].resource_id : null
    eventhubs_private_endpoint_resource_id = var.eventhubs_enabled ? module.eventhubs[0].private_endpoints.eventhubs.id : null
  }
}

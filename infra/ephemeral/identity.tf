locals {
  workload_roles = {
    probe_events_receiver              = { workload = "probe", role_definition_name = "Azure Event Hubs Data Receiver", scope = module.eventhubs.resource_eventhubs["document-events"].id }
    probe_single_action_contributor    = { workload = "probe", role_definition_name = "Storage Blob Data Contributor", scope = module.storage.containers.single_action.id }
    verifier_single_action_reader      = { workload = "verifier", role_definition_name = "Storage Blob Data Reader", scope = module.storage.containers.single_action.id }
    loadgen_events_sender              = { workload = "loadgen", role_definition_name = "Azure Event Hubs Data Sender", scope = module.eventhubs.resource_eventhubs["document-events"].id }
    loadgen_ledger_contributor         = { workload = "loadgen", role_definition_name = "Storage Blob Data Contributor", scope = module.storage.containers.ledger.id }
    enrichment_events_receiver         = { workload = "enrichment-consumer", role_definition_name = "Azure Event Hubs Data Receiver", scope = module.eventhubs.resource_eventhubs["document-events"].id }
    enrichment_state_sender            = { workload = "enrichment-consumer", role_definition_name = "Azure Event Hubs Data Sender", scope = azurerm_eventhub.state.id }
    enrichment_dlq_sender              = { workload = "enrichment-consumer", role_definition_name = "Azure Event Hubs Data Sender", scope = module.eventhubs.resource_eventhubs["document-events-dlq"].id }
    enrichment_claim_check_contributor = { workload = "enrichment-consumer", role_definition_name = "Storage Blob Data Contributor", scope = module.storage.containers.claim_check.id }
    verifier_events_receiver           = { workload = "verifier", role_definition_name = "Azure Event Hubs Data Receiver", scope = module.eventhubs.resource_eventhubs["document-events"].id }
    verifier_ledger_reader             = { workload = "verifier", role_definition_name = "Storage Blob Data Reader", scope = module.storage.containers.ledger.id }
    verifier_results_contributor       = { workload = "verifier", role_definition_name = "Storage Blob Data Contributor", scope = module.storage.containers.verifier.id }
  }
}

resource "azurerm_role_assignment" "workload" {
  for_each                         = local.workload_roles
  role_definition_name             = each.value.role_definition_name
  principal_id                     = local.persistent.workload_identities[each.value.workload].principal_id
  scope                            = each.value.scope
  principal_type                   = "ServicePrincipal"
  skip_service_principal_aad_check = true
}

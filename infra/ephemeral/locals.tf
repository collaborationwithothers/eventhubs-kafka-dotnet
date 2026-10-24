locals {
  persistent = data.terraform_remote_state.persistent.outputs
  names = {
    resource_group             = "rg-parchwell-eph"
    eventhub_namespace         = var.eventhub_namespace_name
    storage                    = "stparchwell${var.naming_suffix}"
    eventhubs_private_endpoint = "pe-eventhubs-${var.naming_suffix}"
    blob_private_endpoint      = "pe-blob-${var.naming_suffix}"
    diagnostic                 = "diag-eventhubs-${var.naming_suffix}"
  }
  tags = {
    repo         = "eventhubs-kafka-dotnet"
    layer        = "ephemeral"
    scenario     = var.scenario_id
    "run-id"     = var.run_id
    "expires-at" = var.expires_at
  }
}

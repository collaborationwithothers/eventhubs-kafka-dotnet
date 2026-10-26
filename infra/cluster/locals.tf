locals {
  persistent = data.terraform_remote_state.persistent.outputs
  workloads  = toset(["loadgen", "enrichment-consumer", "verifier", "probe"])
  names = {
    resource_group     = "rg-parchwell-cluster"
    aks                = "aks-parchwell-${var.naming_suffix}"
    container_insights = "MSCI-${var.location}-aks-parchwell-${var.naming_suffix}"
  }
  tags = {
    repo         = "eventhubs-kafka-dotnet"
    layer        = "cluster"
    scenario     = "shared"
    "run-id"     = "cluster"
    "expires-at" = "never"
  }
}

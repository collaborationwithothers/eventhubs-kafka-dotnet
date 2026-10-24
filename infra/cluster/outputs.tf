output "aks" {
  description = "AKS identity and fixed pool names for scenario deployment."
  value       = { id = module.aks.resource_id, name = local.names.aks, node_pools = ["system", "workload", "loadgen"] }
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

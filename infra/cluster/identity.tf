resource "azurerm_federated_identity_credential" "workload" {
  for_each                  = local.workloads
  name                      = "fic-${each.key}"
  user_assigned_identity_id = local.persistent.workload_identities[each.key].id
  audience                  = ["api://AzureADTokenExchange"]
  issuer                    = module.aks.oidc_issuer_profile_issuer_url
  subject                   = "system:serviceaccount:parchwell:${each.key}"
}

resource "azurerm_role_assignment" "acr_pull" {
  scope                = local.persistent.acr.id
  role_definition_name = "AcrPull"
  principal_id         = module.aks.kubelet_identity.objectId
}

data "azurerm_user_assigned_identity" "aks" {
  name                = basename(local.persistent.aks_identity_id)
  resource_group_name = split("/", local.persistent.aks_identity_id)[4]
}

resource "azurerm_role_assignment" "aks_network" {
  scope                = local.persistent.aks_subnet_id
  role_definition_name = "Network Contributor"
  principal_id         = data.azurerm_user_assigned_identity.aks.principal_id
}

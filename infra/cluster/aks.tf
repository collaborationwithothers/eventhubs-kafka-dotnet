locals {
  aks_config = {
    name               = local.names.aks
    location           = var.location
    parent_id          = azurerm_resource_group.cluster.id
    dns_prefix         = local.names.aks
    kubernetes_version = var.kubernetes_version
    tags               = local.tags
    enable_telemetry   = false
    onboard_monitoring = false
    onboard_alerts     = false
    addon_profile_oms_agent = {
      enabled = true
      config = {
        log_analytics_workspace_resource_id = local.persistent.log_analytics_workspace_id
        use_aad_auth                        = true
      }
    }
    sku                       = { name = "Base", tier = "Free" }
    enable_rbac               = true
    disable_local_accounts    = true
    aad_profile               = { managed = true, enable_azure_rbac = true }
    managed_identities        = { system_assigned = false, user_assigned_resource_ids = [local.persistent.aks_identity_id] }
    api_server_access_profile = { authorized_ip_ranges = var.aks_authorized_ip_ranges, enable_private_cluster = false }
    public_network_access     = "Enabled"
    oidc_issuer_profile       = { enabled = true }
    security_profile          = { workload_identity = { enabled = true } }
    network_profile = {
      network_plugin    = "azure", network_plugin_mode = "overlay"
      network_dataplane = "cilium", network_policy = "cilium"
      load_balancer_sku = "standard", outbound_type = "loadBalancer"
      pod_cidr          = var.pod_cidr, service_cidr = var.service_cidr, dns_service_ip = var.dns_service_ip
    }
    default_agent_pool = {
      name                = "system", count_of = 1, vm_size = "Standard_D4ds_v5"
      enable_auto_scaling = false, enable_node_public_ip = false
      scale_set_priority  = "Regular", type = "VirtualMachineScaleSets"
      vnet_subnet_id      = local.persistent.aks_subnet_id
      node_taints         = ["CriticalAddonsOnly=true:NoSchedule"]
      node_labels         = { workload = "system" }
    }
    agent_pools = {
      workload = {
        name                = "workload", count_of = 1, mode = "User", os_type = "Linux", vm_size = "Standard_D4ds_v5"
        enable_auto_scaling = false, enable_node_public_ip = false
        scale_set_priority  = "Regular", type = "VirtualMachineScaleSets"
        vnet_subnet_id      = local.persistent.aks_subnet_id
        node_taints         = [], node_labels = { workload = "workload" }, tags = local.tags
      }
      loadgen = {
        name                = "loadgen", count_of = 1, mode = "User", os_type = "Linux", vm_size = "Standard_D4ds_v5"
        enable_auto_scaling = false, enable_node_public_ip = false
        scale_set_priority  = "Regular", type = "VirtualMachineScaleSets"
        vnet_subnet_id      = local.persistent.aks_subnet_id
        node_taints         = ["workload=loadgen:NoSchedule"], node_labels = { workload = "loadgen" }, tags = local.tags
      }
    }

  }
}

module "aks" {
  source                    = "Azure/avm-res-containerservice-managedcluster/azurerm"
  version                   = "0.8.5"
  name                      = local.aks_config.name
  location                  = local.aks_config.location
  parent_id                 = local.aks_config.parent_id
  dns_prefix                = local.aks_config.dns_prefix
  kubernetes_version        = local.aks_config.kubernetes_version
  tags                      = local.aks_config.tags
  enable_telemetry          = local.aks_config.enable_telemetry
  onboard_monitoring        = local.aks_config.onboard_monitoring
  onboard_alerts            = local.aks_config.onboard_alerts
  addon_profile_oms_agent   = local.aks_config.addon_profile_oms_agent
  sku                       = local.aks_config.sku
  enable_rbac               = local.aks_config.enable_rbac
  disable_local_accounts    = local.aks_config.disable_local_accounts
  aad_profile               = local.aks_config.aad_profile
  managed_identities        = local.aks_config.managed_identities
  api_server_access_profile = local.aks_config.api_server_access_profile
  public_network_access     = local.aks_config.public_network_access
  oidc_issuer_profile       = local.aks_config.oidc_issuer_profile
  security_profile          = local.aks_config.security_profile
  network_profile           = local.aks_config.network_profile
  default_agent_pool        = local.aks_config.default_agent_pool
  agent_pools               = local.aks_config.agent_pools

  depends_on = [azurerm_role_assignment.aks_network]
}

# Synthetic fixtures only. These plans prove configuration, not Azure readiness.

mock_provider "azurerm" {
  override_during = plan
  mock_resource "azurerm_role_assignment" {
    defaults = {
      "id"                 = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-cluster/providers/Microsoft.Authorization/roleAssignments/00000000-0000-0000-0000-000000000030"
      "role_definition_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/providers/Microsoft.Authorization/roleDefinitions/00000000-0000-0000-0000-000000000031"
    }
  }
  mock_resource "azurerm_federated_identity_credential" {
    defaults = {
      "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-loadgen-offline1/federatedIdentityCredentials/fic-loadgen"
    }
  }

}

mock_provider "azapi" {
  override_during = plan
  mock_resource "azapi_resource" {
    defaults = { output = {} }
  }
}

mock_provider "random" {
  override_during = plan
}

mock_provider "modtm" {
  override_during = plan
}

mock_provider "time" {
  override_during = plan
}

variables {
  naming_suffix            = "offline1"
  location                 = "uksouth"
  pod_cidr                 = "10.242.0.0/16"
  service_cidr             = "10.243.0.0/16"
  dns_service_ip           = "10.243.0.10"
  aks_authorized_ip_ranges = ["192.0.2.10/32"]
  kubernetes_version       = "1.35.0"
  persistent_backend_config = {
    "storage_account_name" = "syntheticstate"
    "container_name"       = "tfstate"
    "key"                  = "persistent.tfstate"
    "resource_group_name"  = "rg-synthetic-backend"
  }
}

override_data {
  target          = data.terraform_remote_state.persistent
  override_during = plan
  values = {
    "outputs" = {
      "spoke_vnet_id"              = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1"
      "aks_subnet_id"              = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/subnets/snet-aks"
      "private_endpoint_subnet_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/subnets/snet-private-endpoints"
      "private_dns_zone_ids" = {
        "eventhubs" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net"
        "blob"      = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net"
      }
      "log_analytics_workspace_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.OperationalInsights/workspaces/law-parchwell-offline1"
      "workload_identities" = {
        "loadgen" = {
          "id"           = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-loadgen-offline1"
          "client_id"    = "00000000-0000-0000-0000-000000000001"
          "principal_id" = "00000000-0000-0000-0000-000000000004"
        }
        "enrichment-consumer" = {
          "id"           = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-enrichment-consumer-offline1"
          "client_id"    = "00000000-0000-0000-0000-000000000002"
          "principal_id" = "00000000-0000-0000-0000-000000000005"
        }
        "verifier" = {
          "id"           = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-verifier-offline1"
          "client_id"    = "00000000-0000-0000-0000-000000000003"
          "principal_id" = "00000000-0000-0000-0000-000000000006"
        }
        "probe" = {
          "id"           = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-probe-offline1"
          "client_id"    = "00000000-0000-0000-0000-000000000012"
          "principal_id" = "00000000-0000-0000-0000-000000000013"
        }
      }
      "aks_identity_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-aks-offline1"
      "acr" = {
        "id"           = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ContainerRegistry/registries/acrparchwelloffline1"
        "name"         = "acrparchwelloffline1"
        "login_server" = "acrparchwelloffline1.azurecr.io"
      }
    }
  }
}

override_resource {
  target          = azurerm_resource_group.cluster
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-cluster"
  }
}

override_resource {
  target          = module.aks.azapi_resource.this
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-cluster/providers/Microsoft.ContainerService/managedClusters/aks-parchwell-offline1"
    "output" = {
      "properties" = {
        "currentKubernetesVersion" = "1.35.0"
        "fqdn"                     = "aks-synthetic.example.invalid"
        "privateFQDN"              = null
        "maxAgentPools"            = 100
        "nodeResourceGroup"        = "rg-synthetic-nodes"
        "oidcIssuerProfile" = {
          "issuerURL" = "https://synthetic.example.invalid/issuer/"
        }
        "identityProfile" = {
          "kubeletidentity" = {
            "clientId"   = "00000000-0000-0000-0000-000000000010"
            "objectId"   = "00000000-0000-0000-0000-000000000011"
            "resourceId" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-synthetic-nodes/providers/Microsoft.ManagedIdentity/userAssignedIdentities/kubelet-synthetic"
          }
        }
        "addonProfiles" = {
          "ingressApplicationGateway" = {
            "identity" = null
          }
          "azureKeyvaultSecretsProvider" = {
            "identity" = null
          }
        }
        "ingressProfile" = {
          "applicationLoadBalancer" = {
            "identity" = {

            }
          }
          "webAppRouting" = {
            "identity" = {

            }
          }
        }
        "networkProfile" = {
          "loadBalancerProfile" = {
            "effectiveOutboundIPs" = []
          }
          "natGatewayProfile" = {
            "effectiveOutboundIPs" = []
          }
        }
      }
    }
  }
}

override_resource {
  target          = module.aks.azapi_resource_action.this_user_kubeconfig[0]
  override_during = plan
  values = {
    "output" = {
      "kubeconfigs" = [{
        "name"  = "synthetic"
        "value" = "YXBpVmVyc2lvbjogdjEKa2luZDogQ29uZmlnCmNsdXN0ZXJzOgotIG5hbWU6IHN5bnRoZXRpYwogIGNsdXN0ZXI6CiAgICBzZXJ2ZXI6IGh0dHBzOi8vYWtzLXN5bnRoZXRpYy5leGFtcGxlLmludmFsaWQKICAgIGNlcnRpZmljYXRlLWF1dGhvcml0eS1kYXRhOiBjM2x1ZEdobGRHbGpMV05oCmNvbnRleHRzOiBbXQp1c2VyczogW10K"
      }]
    }
  }
}

override_resource {
  target          = module.aks.module.nodepools["workload"].azapi_resource.this[0]
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-cluster/providers/Microsoft.ContainerService/managedClusters/aks-parchwell-offline1/agentPools/workload"
    "output" = {

    }
  }
}

override_resource {
  target          = module.aks.module.nodepools["loadgen"].azapi_resource.this[0]
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-cluster/providers/Microsoft.ContainerService/managedClusters/aks-parchwell-offline1/agentPools/loadgen"
    "output" = {

    }
  }
}

override_data {
  target          = data.azurerm_user_assigned_identity.aks
  override_during = plan
  values = {
    id           = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-aks-offline1"
    client_id    = "00000000-0000-0000-0000-000000000007"
    principal_id = "00000000-0000-0000-0000-000000000008"
  }
}

override_resource {
  target          = module.container_insights.azapi_resource.this
  override_during = plan
  values = {
    id     = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-cluster/providers/Microsoft.Insights/dataCollectionRules/MSCI-uksouth-aks-parchwell-offline1"
    output = {}
  }
}

run "foundation" {
  command = plan

  assert {
    condition = jsonencode(module.container_insights.resource.body.properties) == jsonencode({
      description = "DCR for Azure Monitor Container Insights"
      dataSources = {
        dataImports  = null
        etwProviders = null
        extensions = [{
          name             = "ContainerInsightsExtension"
          extensionName    = "ContainerInsights"
          inputDataSources = null
          streams          = ["Microsoft-ContainerInsights-Group-Default"]
          extensionSettings = {
            dataCollectionSettings = {
              interval               = "1m"
              namespaceFilteringMode = "Off"
              namespaces             = []
              enableContainerLogV2   = true
            }
          }
        }]
        iisLogs                 = null
        logFiles                = null
        otelLogs                = null
        otelMetrics             = null
        otelTraces              = null
        performanceCounters     = null
        performanceCountersOTel = null
        platformTelemetry       = null
        prometheusForwarder     = null
        syslog                  = null
        windowsEventLogs        = null
        windowsFirewallLogs     = null
      }
      destinations = {
        azureDataExplorer   = null
        azureMonitorMetrics = null
        eventHubs           = null
        eventHubsDirect     = null
        logAnalytics = [{
          name                = "ciworkspace"
          workspaceResourceId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.OperationalInsights/workspaces/law-parchwell-offline1"
        }]
        microsoftFabric     = null
        monitoringAccounts  = null
        storageAccounts     = null
        storageBlobsDirect  = null
        storageTablesDirect = null
      }
      dataFlows = [{
        streams          = ["Microsoft-ContainerInsights-Group-Default"]
        destinations     = ["ciworkspace"]
        builtInTransform = null
        captureOverflow  = null
        outputStream     = null
        transformKql     = null
      }]
    })
    error_message = "The real DCR body must contain only default Container Insights collection, workspace routing and no additional source, transform or endpoint."
  }

  assert {
    condition     = module.container_insights.resource.name == "MSCI-uksouth-aks-parchwell-offline1" && module.container_insights.resource.location == "uksouth" && module.container_insights.resource.parent_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-cluster" && module.container_insights.resource.body.kind == "Linux" && jsonencode(module.container_insights.resource.tags) == jsonencode(local.tags)
    error_message = "Container Insights must belong to the reusable cluster group with the exact name, region, kind and tags."
  }

  assert {
    condition     = azurerm_monitor_data_collection_rule_association.container_insights.name == "ContainerInsightsExtension" && azurerm_monitor_data_collection_rule_association.container_insights.target_resource_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-cluster/providers/Microsoft.ContainerService/managedClusters/aks-parchwell-offline1" && azurerm_monitor_data_collection_rule_association.container_insights.data_collection_rule_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-cluster/providers/Microsoft.Insights/dataCollectionRules/MSCI-uksouth-aks-parchwell-offline1"
    error_message = "The association must connect the actual cluster and Container Insights DCR producers."
  }

  assert {
    condition     = jsonencode(local.tags) == jsonencode({ repo = "eventhubs-kafka-dotnet", layer = "cluster", scenario = "shared", "run-id" = "cluster", "expires-at" = "never" }) && jsonencode(azurerm_resource_group.cluster.tags) == jsonencode(local.tags) && azurerm_resource_group.cluster.name == "rg-parchwell-cluster"
    error_message = "The reusable cluster group must retain its exact name and full shared cluster tag map."
  }

  assert {
    condition     = data.azurerm_user_assigned_identity.aks.name == "uami-aks-offline1" && data.azurerm_user_assigned_identity.aks.resource_group_name == "rg-parchwell-persist"
    error_message = "The control-plane identity lookup must use the supplied persistent identity resource ID."
  }

  assert {
    condition = jsonencode(local.aks_config) == jsonencode({
      "name"               = "aks-parchwell-offline1"
      "location"           = "uksouth"
      "parent_id"          = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-cluster"
      "dns_prefix"         = "aks-parchwell-offline1"
      "kubernetes_version" = "1.35.0"
      "tags" = {
        "repo"       = "eventhubs-kafka-dotnet"
        "layer"      = "cluster"
        "scenario"   = "shared"
        "run-id"     = "cluster"
        "expires-at" = "never"
      }
      "onboard_monitoring" = false
      "onboard_alerts"     = false
      "addon_profile_oms_agent" = {
        "enabled" = true
        "config" = {
          "log_analytics_workspace_resource_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.OperationalInsights/workspaces/law-parchwell-offline1"
          "use_aad_auth"                        = true
        }
      }
      "enable_telemetry" = false
      "sku" = {
        "name" = "Base"
        "tier" = "Free"
      }
      "enable_rbac"            = true
      "disable_local_accounts" = true
      "aad_profile" = {
        "managed"           = true
        "enable_azure_rbac" = true
      }
      "managed_identities" = {
        "system_assigned"            = false
        "user_assigned_resource_ids" = ["/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-aks-offline1"]
      }
      "api_server_access_profile" = {
        "authorized_ip_ranges"   = ["192.0.2.10/32"]
        "enable_private_cluster" = false
      }
      "public_network_access" = "Enabled"
      "oidc_issuer_profile" = {
        "enabled" = true
      }
      "security_profile" = {
        "workload_identity" = {
          "enabled" = true
        }
      }
      "network_profile" = {
        "network_plugin"      = "azure"
        "network_plugin_mode" = "overlay"
        "network_dataplane"   = "cilium"
        "network_policy"      = "cilium"
        "load_balancer_sku"   = "standard"
        "outbound_type"       = "loadBalancer"
        "pod_cidr"            = "10.242.0.0/16"
        "service_cidr"        = "10.243.0.0/16"
        "dns_service_ip"      = "10.243.0.10"
      }
      "default_agent_pool" = {
        "name"                  = "system"
        "count_of"              = 1
        "vm_size"               = "Standard_D4ds_v5"
        "enable_auto_scaling"   = false
        "enable_node_public_ip" = false
        "scale_set_priority"    = "Regular"
        "type"                  = "VirtualMachineScaleSets"
        "vnet_subnet_id"        = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/subnets/snet-aks"
        "node_taints"           = ["CriticalAddonsOnly=true:NoSchedule"]
        "node_labels" = {
          "workload" = "system"
        }
      }
      "agent_pools" = {
        "workload" = {
          "name"                  = "workload"
          "count_of"              = 1
          "mode"                  = "User"
          "os_type"               = "Linux"
          "vm_size"               = "Standard_D4ds_v5"
          "enable_auto_scaling"   = false
          "enable_node_public_ip" = false
          "scale_set_priority"    = "Regular"
          "type"                  = "VirtualMachineScaleSets"
          "vnet_subnet_id"        = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/subnets/snet-aks"
          "node_taints"           = []
          "node_labels" = {
            "workload" = "workload"
          }
          "tags" = {
            "repo"       = "eventhubs-kafka-dotnet"
            "layer"      = "cluster"
            "scenario"   = "shared"
            "run-id"     = "cluster"
            "expires-at" = "never"
          }
        }
        "loadgen" = {
          "name"                  = "loadgen"
          "count_of"              = 1
          "mode"                  = "User"
          "os_type"               = "Linux"
          "vm_size"               = "Standard_D4ds_v5"
          "enable_auto_scaling"   = false
          "enable_node_public_ip" = false
          "scale_set_priority"    = "Regular"
          "type"                  = "VirtualMachineScaleSets"
          "vnet_subnet_id"        = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/subnets/snet-aks"
          "node_taints"           = ["workload=loadgen:NoSchedule"]
          "node_labels" = {
            "workload" = "loadgen"
          }
          "tags" = {
            "repo"       = "eventhubs-kafka-dotnet"
            "layer"      = "cluster"
            "scenario"   = "shared"
            "run-id"     = "cluster"
            "expires-at" = "never"
          }
        }
      }
    })
    error_message = "AKS module inputs must preserve the exact identity/network/security contract and three isolated fixed pools."
  }

  assert {
    condition     = module.aks.resource_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-cluster/providers/Microsoft.ContainerService/managedClusters/aks-parchwell-offline1" && module.aks.kubelet_identity.objectId == "00000000-0000-0000-0000-000000000011" && module.aks.oidc_issuer_profile_issuer_url == "https://synthetic.example.invalid/issuer/"
    error_message = "AKS supported outputs must use the synthetic actual producer paths."
  }

  assert {
    condition = jsonencode({ for key, credential in azurerm_federated_identity_credential.workload : key => { subject = credential.subject, audience = credential.audience, issuer = credential.issuer, user_assigned_identity_id = credential.user_assigned_identity_id } }) == jsonencode({
      "loadgen" = {
        "subject"                   = "system:serviceaccount:parchwell:loadgen"
        "audience"                  = ["api://AzureADTokenExchange"]
        "issuer"                    = "https://synthetic.example.invalid/issuer/"
        "user_assigned_identity_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-loadgen-offline1"
      }
      "enrichment-consumer" = {
        "subject"                   = "system:serviceaccount:parchwell:enrichment-consumer"
        "audience"                  = ["api://AzureADTokenExchange"]
        "issuer"                    = "https://synthetic.example.invalid/issuer/"
        "user_assigned_identity_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-enrichment-consumer-offline1"
      }
      "verifier" = {
        "subject"                   = "system:serviceaccount:parchwell:verifier"
        "audience"                  = ["api://AzureADTokenExchange"]
        "issuer"                    = "https://synthetic.example.invalid/issuer/"
        "user_assigned_identity_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-verifier-offline1"
      }
      "probe" = {
        "subject"                   = "system:serviceaccount:parchwell:probe"
        "audience"                  = ["api://AzureADTokenExchange"]
        "issuer"                    = "https://synthetic.example.invalid/issuer/"
        "user_assigned_identity_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-probe-offline1"
      }
    })
    error_message = "Federation must bind exactly the four workload subjects to their corresponding persistent identities."
  }

  assert {
    condition     = azurerm_role_assignment.acr_pull.scope == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ContainerRegistry/registries/acrparchwelloffline1" && azurerm_role_assignment.acr_pull.role_definition_name == "AcrPull" && azurerm_role_assignment.acr_pull.principal_id == "00000000-0000-0000-0000-000000000011"
    error_message = "Only the AKS kubelet identity receives registry pull access."
  }

  assert {
    condition = jsonencode(output.aks) == jsonencode({
      "id"         = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-cluster/providers/Microsoft.ContainerService/managedClusters/aks-parchwell-offline1"
      "name"       = "aks-parchwell-offline1"
      "node_pools" = ["system", "workload", "loadgen"]
    })
    error_message = "AKS output must expose exactly the identity, name and fixed pool list."
  }

  assert {
    condition = jsonencode(output.workload_identity) == jsonencode({
      "namespace" = "parchwell"
      "service_accounts" = {
        "loadgen" = {
          "name"      = "loadgen"
          "client_id" = "00000000-0000-0000-0000-000000000001"
          "subject"   = "system:serviceaccount:parchwell:loadgen"
        }
        "enrichment-consumer" = {
          "name"      = "enrichment-consumer"
          "client_id" = "00000000-0000-0000-0000-000000000002"
          "subject"   = "system:serviceaccount:parchwell:enrichment-consumer"
        }
        "verifier" = {
          "name"      = "verifier"
          "client_id" = "00000000-0000-0000-0000-000000000003"
          "subject"   = "system:serviceaccount:parchwell:verifier"
        }
        "probe" = {
          "name"      = "probe"
          "client_id" = "00000000-0000-0000-0000-000000000012"
          "subject"   = "system:serviceaccount:parchwell:probe"
        }
      }
    })
    error_message = "Worker deployment output must expose exactly the namespace and four service-account contracts."
  }

  assert {
    condition     = azurerm_role_assignment.aks_network.scope == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/subnets/snet-aks" && azurerm_role_assignment.aks_network.principal_id == "00000000-0000-0000-0000-000000000008" && azurerm_role_assignment.aks_network.role_definition_name == "Network Contributor"
    error_message = "Control-plane Network Contributor must remain scoped to the AKS subnet."
  }

  assert {
    condition = jsonencode(data.terraform_remote_state.persistent.config) == jsonencode({
      "storage_account_name" = "syntheticstate"
      "container_name"       = "tfstate"
      "key"                  = "persistent.tfstate"
      "resource_group_name"  = "rg-synthetic-backend"
      "use_azuread_auth"     = true
      "use_oidc"             = true
    })
    error_message = "Remote state must use exactly the supplied backend location and Entra/OIDC authentication flags."
  }
}

run "remote_state_cli" {
  command = plan
  variables {
    persistent_backend_use_oidc = false
  }

  assert {
    condition = jsonencode(data.terraform_remote_state.persistent.config) == jsonencode({
      "storage_account_name" = "syntheticstate"
      "container_name"       = "tfstate"
      "key"                  = "persistent.tfstate"
      "resource_group_name"  = "rg-synthetic-backend"
      "use_azuread_auth"     = true
      "use_oidc"             = false
    })
    error_message = "Remote state must use exactly the supplied backend location and Entra/non-OIDC authentication flags."
  }
}

run "reject_suffix" {
  command = plan
  variables {
    naming_suffix = "BAD_SUFFIX"
  }
  expect_failures = [var.naming_suffix]
}

run "reject_location" {
  command = plan
  variables {
    location = "ukwest"
  }
  expect_failures = [var.location]
}

run "reject_backend" {
  command = plan
  variables {
    persistent_backend_config = {
      "storage_account_name" = ""
      "container_name"       = "tfstate"
      "key"                  = "persistent.tfstate"
      "resource_group_name"  = "rg-synthetic-backend"
    }
  }
  expect_failures = [var.persistent_backend_config]
}

run "reject_pod_cidr" {
  command = plan
  variables {
    pod_cidr = "not-cidr"
  }
  expect_failures = [var.pod_cidr]
}

run "reject_service_cidr" {
  command = plan
  variables {
    service_cidr = "not-cidr"
  }
  expect_failures = [var.service_cidr]
}

run "reject_dns_ip" {
  command = plan
  variables {
    dns_service_ip = "not-ip"
  }
  expect_failures = [var.dns_service_ip]
}

run "reject_empty_ranges" {
  command = plan
  variables {
    aks_authorized_ip_ranges = []
  }
  expect_failures = [var.aks_authorized_ip_ranges]
}

run "reject_bad_ranges" {
  command = plan
  variables {
    aks_authorized_ip_ranges = ["not-cidr"]
  }
  expect_failures = [var.aks_authorized_ip_ranges]
}

run "reject_version" {
  command = plan
  variables {
    kubernetes_version = "latest"
  }
  expect_failures = [var.kubernetes_version]
}

run "reject_below_version_floor" {
  command = plan
  variables {
    kubernetes_version = "1.34.99"
  }
  expect_failures = [var.kubernetes_version]
}

run "reject_lower_major" {
  command = plan
  variables {
    kubernetes_version = "0.99.0"
  }
  expect_failures = [var.kubernetes_version]
}

# Synthetic fixtures only. These plans prove configuration, not Azure readiness.

mock_provider "azurerm" {
  override_during = plan
  mock_resource "azurerm_role_assignment" {
    defaults = {
      "id"                 = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Authorization/roleAssignments/00000000-0000-0000-0000-000000000030"
      "role_definition_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/providers/Microsoft.Authorization/roleDefinitions/00000000-0000-0000-0000-000000000031"
    }
  }
  mock_resource "azurerm_federated_identity_credential" {
    defaults = {
      "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-loadgen-offline1/federatedIdentityCredentials/fic-loadgen"
    }
  }
  mock_resource "azurerm_monitor_diagnostic_setting" {
    defaults = {
      "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1|diag-eventhubs-offline1"
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
  eventhub_namespace_name  = "eh-parchwell-offline1"
  scenario_id              = "A01"
  run_id                   = "offline1"
  expires_at               = "2026-11-01T04:00:00Z"
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
  target          = azurerm_resource_group.ephemeral
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph"
  }
}

override_resource {
  target          = module.eventhubs.azurerm_eventhub_namespace.this[0]
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1"
  }
}

override_resource {
  target          = module.eventhubs.azurerm_eventhub.this["document-events"]
  override_during = plan
  values = {
    "id"            = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events"
    "partition_ids" = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "30", "31"]
  }
}

override_resource {
  target          = module.eventhubs.azurerm_eventhub.this["document-events-dlq"]
  override_during = plan
  values = {
    "id"            = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events-dlq"
    "partition_ids" = ["0", "1", "2", "3"]
  }
}

override_resource {
  target          = azurerm_eventhub.state
  override_during = plan
  values = {
    "id"            = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-state"
    "partition_ids" = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "30", "31"]
  }
}

override_resource {
  target          = module.eventhubs.azurerm_private_endpoint.this["eventhubs"]
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Network/privateEndpoints/pe-eventhubs-offline1"
    "private_service_connection" = {
      "name"                              = "pse-eh-parchwell-offline1"
      "private_connection_resource_id"    = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1"
      "subresource_names"                 = ["namespace"]
      "is_manual_connection"              = false
      "private_connection_resource_alias" = null
      "request_message"                   = null
      "private_ip_address"                = "10.240.4.4"
    }
  }
}

override_resource {
  target          = module.storage.azapi_resource.this
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1"
    "output" = {
      "properties" = {
        "primaryEndpoints" = {
          "blob" = "https://stparchwelloffline1.blob.core.windows.net/"
        }
        "secondaryEndpoints" = {

        }
        "primaryLocation"   = "uksouth"
        "secondaryLocation" = null
      }
    }
  }
}

override_resource {
  target          = module.storage.module.containers["ledger"].azapi_resource.this
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/ledger"
    "output" = {

    }
  }
}

override_resource {
  target          = module.storage.module.containers["verifier"].azapi_resource.this
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/verifier"
    "output" = {

    }
  }
}

override_resource {
  target          = module.storage.module.containers["claim_check"].azapi_resource.this
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/claim-check"
    "output" = {

    }
  }
}

override_resource {
  target          = module.storage.module.private_endpoints["blob"].azapi_resource.this
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Network/privateEndpoints/pe-blob-offline1"
    "output" = {

    }
  }
}

override_resource {
  target          = module.storage.module.private_endpoints["blob"].azapi_resource.private_dns_zone_group[0]
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Network/privateEndpoints/pe-blob-offline1/privateDnsZoneGroups/default"
    "output" = {

    }
  }
}

override_data {
  target          = data.azurerm_private_endpoint_connection.blob
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Network/privateEndpoints/pe-blob-offline1"
    "network_interface" = [{
      "id"   = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Network/networkInterfaces/nic-blob-offline1"
      "name" = "nic-blob-offline1"
    }]
    "private_service_connection" = [{
      "name"               = "blob"
      "request_response"   = ""
      "status"             = "Approved"
      "private_ip_address" = "10.240.4.5"
    }]
  }
}

override_resource {
  target          = module.application_insights.azurerm_application_insights.this
  override_during = plan
  values = {
    "id"                  = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Insights/components/appi-parchwell-offline1"
    "app_id"              = "00000000-0000-0000-0000-000000000021"
    "instrumentation_key" = "00000000-0000-0000-0000-000000000022"
    "connection_string"   = "InstrumentationKey=00000000-0000-0000-0000-000000000022;IngestionEndpoint=https://synthetic.example.invalid/"
  }
}

override_resource {
  target          = module.aks.azapi_resource.this
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.ContainerService/managedClusters/aks-parchwell-offline1"
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
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.ContainerService/managedClusters/aks-parchwell-offline1/agentPools/workload"
    "output" = {

    }
  }
}

override_resource {
  target          = module.aks.module.nodepools["loadgen"].azapi_resource.this[0]
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.ContainerService/managedClusters/aks-parchwell-offline1/agentPools/loadgen"
    "output" = {

    }
  }
}

# One positive plan covers the closed configuration contract in plan checks 5-10.
run "foundation" {
  command = plan

  assert {
    condition     = module.eventhubs.resource.sku == "Premium" && module.eventhubs.resource.capacity == 1 && !module.eventhubs.resource.local_authentication_enabled && !module.eventhubs.resource.public_network_access_enabled && module.eventhubs.resource.minimum_tls_version == "1.2"
    error_message = "Event Hubs namespace must retain Premium/1 PU, Entra/private access and TLS 1.2."
  }

  assert {
    condition     = jsonencode(keys(module.eventhubs.resource_eventhubs)) == jsonencode(["document-events", "document-events-dlq"])
    error_message = "Only the two delete topics belong to the AVM."
  }

  assert {
    condition     = module.eventhubs.resource_eventhubs["document-events"].partition_count == 32 && module.eventhubs.resource_eventhubs["document-events-dlq"].partition_count == 4 && alltrue([for hub in module.eventhubs.resource_eventhubs : hub.message_retention == 7 && hub.status == "Active"])
    error_message = "Delete topics must keep fixed partitions, seven-day retention and Active status."
  }

  assert {
    condition     = azurerm_eventhub.state.name == "document-state" && azurerm_eventhub.state.namespace_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1" && azurerm_eventhub.state.partition_count == 32 && azurerm_eventhub.state.retention_description[0].cleanup_policy == "Compact" && azurerm_eventhub.state.retention_description[0].tombstone_retention_time_in_hours == 168 && azurerm_eventhub.state.retention_description[0].retention_time_in_hours == null
    error_message = "The single direct state topic must use Compact and the fixed tombstone period."
  }

  assert {
    condition     = module.eventhubs.private_endpoints.eventhubs.subnet_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/subnets/snet-private-endpoints" && length(module.eventhubs.private_endpoints.eventhubs.private_service_connection) == 1 && module.eventhubs.private_endpoints.eventhubs.private_service_connection[0].name == "pse-eh-parchwell-offline1" && module.eventhubs.private_endpoints.eventhubs.private_service_connection[0].private_connection_resource_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1" && !module.eventhubs.private_endpoints.eventhubs.private_service_connection[0].is_manual_connection && toset(module.eventhubs.private_endpoints.eventhubs.private_service_connection[0].subresource_names) == toset(["namespace"]) && length(module.eventhubs.private_endpoints.eventhubs.private_dns_zone_group) == 1 && toset(module.eventhubs.private_endpoints.eventhubs.private_dns_zone_group[0].private_dns_zone_ids) == toset(["/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net"])
    error_message = "Event Hubs endpoint must target its namespace through the persistent PE subnet and DNS zone."
  }

  assert {
    condition     = module.storage.resource.body.properties.allowSharedKeyAccess == false && module.storage.resource.body.properties.defaultToOAuthAuthentication == true && module.storage.resource.body.properties.publicNetworkAccess == "Disabled" && module.storage.resource.body.properties.minimumTlsVersion == "TLS1_2" && module.storage.resource.body.properties.supportsHttpsTrafficOnly == true && module.storage.resource.body.properties.allowBlobPublicAccess == false
    error_message = "Blob account must retain Entra/private access and HTTPS/TLS restrictions."
  }

  assert {
    condition = jsonencode(local.storage_config) == jsonencode({
      "name"      = "stparchwelloffline1"
      "location"  = "uksouth"
      "parent_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph"
      "tags" = {
        "repo"       = "eventhubs-kafka-dotnet"
        "layer"      = "ephemeral"
        "scenario"   = "A01"
        "run-id"     = "offline1"
        "expires-at" = "2026-11-01T04:00:00Z"
      }
      "enable_telemetry"                = false
      "account_kind"                    = "StorageV2"
      "account_sku_name"                = "Standard_LRS"
      "access_tier"                     = "Hot"
      "public_network_access_enabled"   = false
      "shared_access_key_enabled"       = false
      "default_to_oauth_authentication" = true
      "allow_nested_items_to_be_public" = false
      "https_traffic_only_enabled"      = true
      "min_tls_version"                 = "TLS1_2"
      "role_assignments" = {

      }
      "containers" = {
        "ledger" = {
          "name"          = "ledger"
          "public_access" = "None"
          "role_assignments" = {

          }
        }
        "verifier" = {
          "name"          = "verifier"
          "public_access" = "None"
          "role_assignments" = {

          }
        }
        "claim_check" = {
          "name"          = "claim-check"
          "public_access" = "None"
          "role_assignments" = {

          }
        }
      }
      "private_endpoints_manage_dns_zone_group" = true
      "private_endpoints" = {
        "blob" = {
          "name"                          = "pe-blob-offline1"
          "subnet_resource_id"            = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/subnets/snet-private-endpoints"
          "subresource_name"              = "blob"
          "private_dns_zone_resource_ids" = ["/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net"]
          "tags" = {
            "repo"       = "eventhubs-kafka-dotnet"
            "layer"      = "ephemeral"
            "scenario"   = "A01"
            "run-id"     = "offline1"
            "expires-at" = "2026-11-01T04:00:00Z"
          }
          "role_assignments" = {

          }
        }
      }
    })
    error_message = "Storage configuration must retain exactly the private Blob endpoint, nonpublic containers and empty optional role maps."
  }

  assert {
    condition     = jsonencode(keys(module.storage.private_endpoints)) == jsonencode(["blob"])
    error_message = "Storage must expose exactly its Blob endpoint."
  }

  assert {
    condition = jsonencode(module.storage.containers) == jsonencode({
      "claim_check" = {
        "id"   = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/claim-check"
        "name" = "claim-check"
      }
      "ledger" = {
        "id"   = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/ledger"
        "name" = "ledger"
      }
      "verifier" = {
        "id"   = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/verifier"
        "name" = "verifier"
      }
    })
    error_message = "The three container names and resource IDs must match their distinct producers."
  }

  assert {
    condition = jsonencode(local.aks_config) == jsonencode({
      "name"               = "aks-parchwell-offline1"
      "location"           = "uksouth"
      "parent_id"          = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph"
      "dns_prefix"         = "aks-parchwell-offline1"
      "kubernetes_version" = "1.35.0"
      "tags" = {
        "repo"       = "eventhubs-kafka-dotnet"
        "layer"      = "ephemeral"
        "scenario"   = "A01"
        "run-id"     = "offline1"
        "expires-at" = "2026-11-01T04:00:00Z"
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
            "layer"      = "ephemeral"
            "scenario"   = "A01"
            "run-id"     = "offline1"
            "expires-at" = "2026-11-01T04:00:00Z"
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
            "layer"      = "ephemeral"
            "scenario"   = "A01"
            "run-id"     = "offline1"
            "expires-at" = "2026-11-01T04:00:00Z"
          }
        }
      }
    })
    error_message = "AKS module inputs must preserve the exact identity/network/security contract and three isolated fixed pools."
  }

  assert {
    condition     = module.aks.resource_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.ContainerService/managedClusters/aks-parchwell-offline1" && module.aks.kubelet_identity.objectId == "00000000-0000-0000-0000-000000000011" && module.aks.oidc_issuer_profile_issuer_url == "https://synthetic.example.invalid/issuer/"
    error_message = "AKS supported outputs must use the synthetic actual producer paths."
  }

  assert {
    condition = jsonencode(local.eventhubs_config) == jsonencode({
      "name"                = "eh-parchwell-offline1"
      "location"            = "uksouth"
      "resource_group_name" = "rg-parchwell-eph"
      "tags" = {
        "repo"       = "eventhubs-kafka-dotnet"
        "layer"      = "ephemeral"
        "scenario"   = "A01"
        "run-id"     = "offline1"
        "expires-at" = "2026-11-01T04:00:00Z"
      }
      "enable_telemetry"              = false
      "sku"                           = "Premium"
      "capacity"                      = 1
      "auto_inflate_enabled"          = false
      "maximum_throughput_units"      = null
      "local_authentication_enabled"  = false
      "public_network_access_enabled" = false
      "event_hubs" = {
        "document-events" = {
          "namespace_name"      = "eh-parchwell-offline1"
          "resource_group_name" = "rg-parchwell-eph"
          "partition_count"     = 32
          "message_retention"   = 7
          "status"              = "Active"
          "role_assignments" = {

          }
        }
        "document-events-dlq" = {
          "namespace_name"      = "eh-parchwell-offline1"
          "resource_group_name" = "rg-parchwell-eph"
          "partition_count"     = 4
          "message_retention"   = 7
          "status"              = "Active"
          "role_assignments" = {

          }
        }
      }
      "private_endpoints_manage_dns_zone_group" = true
      "private_endpoints" = {
        "eventhubs" = {
          "name"                            = "pe-eventhubs-offline1"
          "private_service_connection_name" = "pse-eh-parchwell-offline1"
          "subnet_resource_id"              = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/subnets/snet-private-endpoints"
          "private_dns_zone_resource_ids"   = ["/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net"]
          "tags" = {
            "repo"       = "eventhubs-kafka-dotnet"
            "layer"      = "ephemeral"
            "scenario"   = "A01"
            "run-id"     = "offline1"
            "expires-at" = "2026-11-01T04:00:00Z"
          }
        }
      }
    })
    error_message = "Event Hubs inputs must keep the namespace/delete-topic/endpoint ownership contract."
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
    })
    error_message = "Federation must bind exactly the three workload subjects to their corresponding persistent identities."
  }

  assert {
    condition     = toset([for role in azurerm_role_assignment.workload : jsonencode([role.principal_id, role.role_definition_name, role.scope])]) == toset([jsonencode(["00000000-0000-0000-0000-000000000004", "Azure Event Hubs Data Sender", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events"]), jsonencode(["00000000-0000-0000-0000-000000000004", "Storage Blob Data Contributor", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/ledger"]), jsonencode(["00000000-0000-0000-0000-000000000005", "Azure Event Hubs Data Receiver", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events"]), jsonencode(["00000000-0000-0000-0000-000000000005", "Azure Event Hubs Data Sender", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-state"]), jsonencode(["00000000-0000-0000-0000-000000000005", "Azure Event Hubs Data Sender", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events-dlq"]), jsonencode(["00000000-0000-0000-0000-000000000005", "Storage Blob Data Contributor", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/claim-check"]), jsonencode(["00000000-0000-0000-0000-000000000006", "Azure Event Hubs Data Receiver", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events"]), jsonencode(["00000000-0000-0000-0000-000000000006", "Storage Blob Data Reader", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/ledger"]), jsonencode(["00000000-0000-0000-0000-000000000006", "Storage Blob Data Contributor", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/verifier"])]) && length(azurerm_role_assignment.workload) == 9 && alltrue([for role in azurerm_role_assignment.workload : role.principal_type == "ServicePrincipal" && role.skip_service_principal_aad_check])
    error_message = "The complete workload role set must equal the nine approved topic/container grants, with no broader data role."
  }

  assert {
    condition     = azurerm_role_assignment.acr_pull.scope == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ContainerRegistry/registries/acrparchwelloffline1" && azurerm_role_assignment.acr_pull.role_definition_name == "AcrPull" && azurerm_role_assignment.acr_pull.principal_id == "00000000-0000-0000-0000-000000000011"
    error_message = "Only the AKS kubelet identity receives registry pull access."
  }

  assert {
    condition     = module.application_insights.resource.workspace_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.OperationalInsights/workspaces/law-parchwell-offline1" && module.application_insights.resource.application_type == "other" && module.application_insights.resource.internet_ingestion_enabled && module.application_insights.resource.internet_query_enabled
    error_message = "Application Insights must use the persistent workspace and selected public Azure Monitor topology."
  }

  assert {
    condition     = azurerm_monitor_diagnostic_setting.eventhubs.target_resource_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1" && azurerm_monitor_diagnostic_setting.eventhubs.log_analytics_workspace_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.OperationalInsights/workspaces/law-parchwell-offline1" && azurerm_monitor_diagnostic_setting.eventhubs.log_analytics_destination_type == "Dedicated" && length(azurerm_monitor_diagnostic_setting.eventhubs.enabled_log) == 1 && one(azurerm_monitor_diagnostic_setting.eventhubs.enabled_log).category_group == "allLogs" && length(azurerm_monitor_diagnostic_setting.eventhubs.enabled_metric) == 1 && one(azurerm_monitor_diagnostic_setting.eventhubs.enabled_metric).category == "AllMetrics"
    error_message = "Diagnostics must target Event Hubs and the persistent workspace using allLogs/AllMetrics."
  }

  assert {
    condition = alltrue([for actual in [azurerm_resource_group.ephemeral.tags, module.eventhubs.resource.tags, module.storage.resource.tags, module.application_insights.resource.tags, module.eventhubs.private_endpoints.eventhubs.tags] : jsonencode(actual) == jsonencode({
      "repo"       = "eventhubs-kafka-dotnet"
      "layer"      = "ephemeral"
      "scenario"   = "A01"
      "run-id"     = "offline1"
      "expires-at" = "2026-11-01T04:00:00Z"
    })])
    error_message = "All supporting exposed resources must carry exactly the five ephemeral fixture tags."
  }

  assert {
    condition = jsonencode(output.aks) == jsonencode({
      "id"         = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.ContainerService/managedClusters/aks-parchwell-offline1"
      "name"       = "aks-parchwell-offline1"
      "node_pools" = ["system", "workload", "loadgen"]
    })
    error_message = "AKS output must expose exactly the identity, name and fixed pool list."
  }

  assert {
    condition = jsonencode(output.eventhubs) == jsonencode({
      "namespace_name"    = "eh-parchwell-offline1"
      "bootstrap_servers" = "eh-parchwell-offline1.servicebus.windows.net:9093"
      "topics" = {
        "events" = "document-events"
        "state"  = "document-state"
        "dlq"    = "document-events-dlq"
      }
    })
    error_message = "Kafka output must expose the exact namespace, TLS bootstrap address and topic contract."
  }

  assert {
    condition = jsonencode(output.blob) == jsonencode({
      "account_url" = "https://stparchwelloffline1.blob.core.windows.net"
      "containers" = {
        "ledger"      = "ledger"
        "verifier"    = "verifier"
        "claim_check" = "claim-check"
      }
    })
    error_message = "Blob output must expose its HTTPS account URL and exact container keys/names."
  }

  assert {
    condition = jsonencode(output.private_endpoint_ips) == jsonencode({
      "eventhubs" = "10.240.4.4"
      "blob"      = "10.240.4.5"
    })
    error_message = "Private endpoint IP output must use the distinct actual producers."
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
      }
    })
    error_message = "Worker deployment output must expose exactly the namespace and three service-account contracts."
  }

  assert {
    condition     = issensitive(output.application_insights_connection_string) && output.application_insights_connection_string == "InstrumentationKey=00000000-0000-0000-0000-000000000022;IngestionEndpoint=https://synthetic.example.invalid/"
    error_message = "Sensitive Application Insights output must preserve the exact synthetic connection string."
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

# Reject only the named variable; unrelated provider errors must fail this run.
run "reject_suffix" {
  command = plan
  variables {
    naming_suffix = "BAD_SUFFIX"
  }
  expect_failures = [var.naming_suffix]
}

# Reject only the named variable; unrelated provider errors must fail this run.
run "reject_location" {
  command = plan
  variables {
    location = "ukwest"
  }
  expect_failures = [var.location]
}

# Reject only the named variable; unrelated provider errors must fail this run.
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

# Reject only the named variable; unrelated provider errors must fail this run.
run "reject_pod_cidr" {
  command = plan
  variables {
    pod_cidr = "not-cidr"
  }
  expect_failures = [var.pod_cidr]
}

# Reject only the named variable; unrelated provider errors must fail this run.
run "reject_service_cidr" {
  command = plan
  variables {
    service_cidr = "not-cidr"
  }
  expect_failures = [var.service_cidr]
}

# Reject only the named variable; unrelated provider errors must fail this run.
run "reject_dns_ip" {
  command = plan
  variables {
    dns_service_ip = "not-ip"
  }
  expect_failures = [var.dns_service_ip]
}

# Reject only the named variable; unrelated provider errors must fail this run.
run "reject_empty_ranges" {
  command = plan
  variables {
    aks_authorized_ip_ranges = []
  }
  expect_failures = [var.aks_authorized_ip_ranges]
}

# Reject only the named variable; unrelated provider errors must fail this run.
run "reject_bad_ranges" {
  command = plan
  variables {
    aks_authorized_ip_ranges = ["not-cidr"]
  }
  expect_failures = [var.aks_authorized_ip_ranges]
}

# Reject only the named variable; unrelated provider errors must fail this run.
run "reject_version" {
  command = plan
  variables {
    kubernetes_version = "latest"
  }
  expect_failures = [var.kubernetes_version]
}

# Reject only the named variable; unrelated provider errors must fail this run.
run "reject_azure_namespace" {
  command = plan
  variables {
    eventhub_namespace_name = "eh-azure-offline1"
  }
  expect_failures = [var.eventhub_namespace_name]
}

# Reject only the named variable; unrelated provider errors must fail this run.
run "reject_namespace_shape" {
  command = plan
  variables {
    eventhub_namespace_name = "bad_name"
  }
  expect_failures = [var.eventhub_namespace_name]
}

# Reject only the named variable; unrelated provider errors must fail this run.
run "reject_scenario" {
  command = plan
  variables {
    scenario_id = "C01"
  }
  expect_failures = [var.scenario_id]
}

# Reject only the named variable; unrelated provider errors must fail this run.
run "reject_run" {
  command = plan
  variables {
    run_id = ""
  }
  expect_failures = [var.run_id]
}

# Reject only the named variable; unrelated provider errors must fail this run.
run "reject_expiry" {
  command = plan
  variables {
    expires_at = "tomorrow"
  }
  expect_failures = [var.expires_at]
}

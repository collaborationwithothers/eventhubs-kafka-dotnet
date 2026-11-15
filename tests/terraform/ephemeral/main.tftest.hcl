# Synthetic fixtures only. These plans prove configuration, not Azure readiness.

mock_provider "azurerm" {
  override_during = plan
  mock_resource "azurerm_role_assignment" {
    defaults = {
      "id"                 = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Authorization/roleAssignments/00000000-0000-0000-0000-000000000030"
      "role_definition_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/providers/Microsoft.Authorization/roleDefinitions/00000000-0000-0000-0000-000000000031"
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
  naming_suffix           = "offline1"
  location                = "uksouth"
  eventhub_namespace_name = "eh-parchwell-offline1"
  scenario_id             = "A01"
  run_id                  = "offline1"
  expires_at              = "2026-11-01T04:00:00Z"
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
  target          = azurerm_resource_group.ephemeral
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph"
  }
}

override_resource {
  target          = module.eventhubs[0].azurerm_eventhub_namespace.this[0]
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1"
  }
}

override_resource {
  target          = module.eventhubs[0].azurerm_eventhub.this["document-events"]
  override_during = plan
  values = {
    "id"            = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events"
    "partition_ids" = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "30", "31"]
  }
}

override_resource {
  target          = module.eventhubs[0].azurerm_eventhub.this["document-events-dlq"]
  override_during = plan
  values = {
    "id"            = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events-dlq"
    "partition_ids" = ["0", "1", "2", "3"]
  }
}

override_resource {
  target          = azurerm_eventhub.state[0]
  override_during = plan
  values = {
    "id"            = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-state"
    "partition_ids" = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "30", "31"]
  }
}

override_resource {
  target          = module.eventhubs[0].azurerm_private_endpoint.this["eventhubs"]
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
  target          = module.storage.module.containers["single_action"].azapi_resource.this
  override_during = plan
  values = {
    "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/single-action"
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

run "foundation" {
  command = plan
  assert {
    condition     = var.archive_smoke_enabled == false && !contains(keys(local.storage_config.containers), "archive_smoke_source") && !contains(keys(output.blob.containers), "archive_smoke_source")
    error_message = "Default plans must not require the workflow principal or add a smoke source or grant."
  }

  assert {
    condition     = module.eventhubs[0].resource.sku == "Premium" && module.eventhubs[0].resource.capacity == 1 && !module.eventhubs[0].resource.local_authentication_enabled && !module.eventhubs[0].resource.public_network_access_enabled && module.eventhubs[0].resource.minimum_tls_version == "1.2"
    error_message = "Event Hubs namespace must retain Premium/1 PU, Entra/private access and TLS 1.2."
  }

  assert {
    condition     = jsonencode(keys(module.eventhubs[0].resource_eventhubs)) == jsonencode(["document-events", "document-events-dlq"])
    error_message = "Only the two delete topics belong to the AVM."
  }

  assert {
    condition     = module.eventhubs[0].resource_eventhubs["document-events"].partition_count == 32 && module.eventhubs[0].resource_eventhubs["document-events-dlq"].partition_count == 4 && alltrue([for hub in module.eventhubs[0].resource_eventhubs : hub.message_retention == 7 && hub.status == "Active"])
    error_message = "Delete topics must keep fixed partitions, seven-day retention and Active status."
  }

  assert {
    condition     = azurerm_eventhub.state[0].name == "document-state" && azurerm_eventhub.state[0].namespace_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1" && azurerm_eventhub.state[0].partition_count == 32 && azurerm_eventhub.state[0].retention_description[0].cleanup_policy == "Compact" && azurerm_eventhub.state[0].retention_description[0].tombstone_retention_time_in_hours == 168 && azurerm_eventhub.state[0].retention_description[0].retention_time_in_hours == null
    error_message = "The single direct state topic must use Compact and the fixed tombstone period."
  }

  assert {
    condition     = module.eventhubs[0].private_endpoints.eventhubs.subnet_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/subnets/snet-private-endpoints" && length(module.eventhubs[0].private_endpoints.eventhubs.private_service_connection) == 1 && module.eventhubs[0].private_endpoints.eventhubs.private_service_connection[0].name == "pse-eh-parchwell-offline1" && module.eventhubs[0].private_endpoints.eventhubs.private_service_connection[0].private_connection_resource_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1" && !module.eventhubs[0].private_endpoints.eventhubs.private_service_connection[0].is_manual_connection && toset(module.eventhubs[0].private_endpoints.eventhubs.private_service_connection[0].subresource_names) == toset(["namespace"]) && length(module.eventhubs[0].private_endpoints.eventhubs.private_dns_zone_group) == 1 && toset(module.eventhubs[0].private_endpoints.eventhubs.private_dns_zone_group[0].private_dns_zone_ids) == toset(["/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net"])
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
        "single_action" = {
          "name"          = "single-action"
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
      "single_action" = {
        "id"   = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/single-action"
        "name" = "single-action"
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
    error_message = "The four container names and resource IDs must match their distinct producers."
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
    condition     = toset([for role in azurerm_role_assignment.workload : jsonencode([role.principal_id, role.role_definition_name, role.scope])]) == toset([jsonencode(["00000000-0000-0000-0000-000000000004", "Azure Event Hubs Data Sender", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events"]), jsonencode(["00000000-0000-0000-0000-000000000004", "Storage Blob Data Contributor", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/ledger"]), jsonencode(["00000000-0000-0000-0000-000000000005", "Azure Event Hubs Data Receiver", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events"]), jsonencode(["00000000-0000-0000-0000-000000000005", "Azure Event Hubs Data Sender", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-state"]), jsonencode(["00000000-0000-0000-0000-000000000005", "Azure Event Hubs Data Sender", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events-dlq"]), jsonencode(["00000000-0000-0000-0000-000000000005", "Storage Blob Data Contributor", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/claim-check"]), jsonencode(["00000000-0000-0000-0000-000000000006", "Azure Event Hubs Data Receiver", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events"]), jsonencode(["00000000-0000-0000-0000-000000000006", "Storage Blob Data Reader", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/ledger"]), jsonencode(["00000000-0000-0000-0000-000000000006", "Storage Blob Data Contributor", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/verifier"]), jsonencode(["00000000-0000-0000-0000-000000000013", "Azure Event Hubs Data Receiver", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events"]), jsonencode(["00000000-0000-0000-0000-000000000013", "Storage Blob Data Contributor", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/single-action"]), jsonencode(["00000000-0000-0000-0000-000000000006", "Storage Blob Data Reader", "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/single-action"])]) && length(azurerm_role_assignment.workload) == 12 && alltrue([for role in azurerm_role_assignment.workload : role.principal_type == "ServicePrincipal" && role.skip_service_principal_aad_check])
    error_message = "The complete workload role set must equal the twelve approved topic/container grants, with no broader data role."
  }

  assert {
    condition     = azurerm_monitor_diagnostic_setting.eventhubs[0].target_resource_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1" && azurerm_monitor_diagnostic_setting.eventhubs[0].log_analytics_workspace_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.OperationalInsights/workspaces/law-parchwell-offline1" && azurerm_monitor_diagnostic_setting.eventhubs[0].log_analytics_destination_type == "Dedicated" && length(azurerm_monitor_diagnostic_setting.eventhubs[0].enabled_log) == 1 && one(azurerm_monitor_diagnostic_setting.eventhubs[0].enabled_log).category_group == "allLogs" && length(azurerm_monitor_diagnostic_setting.eventhubs[0].enabled_metric) == 1 && one(azurerm_monitor_diagnostic_setting.eventhubs[0].enabled_metric).category == "AllMetrics"
    error_message = "Diagnostics must target Event Hubs and the persistent workspace using allLogs/AllMetrics."
  }

  assert {
    condition = alltrue([for actual in [azurerm_resource_group.ephemeral.tags, module.eventhubs[0].resource.tags, module.storage.resource.tags, module.eventhubs[0].private_endpoints.eventhubs.tags] : jsonencode(actual) == jsonencode({
      "repo"       = "eventhubs-kafka-dotnet"
      "layer"      = "ephemeral"
      "scenario"   = "A01"
      "run-id"     = "offline1"
      "expires-at" = "2026-11-01T04:00:00Z"
    })])
    error_message = "All supporting exposed resources must carry exactly the five ephemeral fixture tags."
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
        "ledger"        = "ledger"
        "verifier"      = "verifier"
        "claim_check"   = "claim-check"
        "single_action" = "single-action"
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

run "reject_azure_namespace" {
  command = plan
  variables {
    eventhub_namespace_name = "eh-azure-offline1"
  }
  expect_failures = [var.eventhub_namespace_name]
}

run "reject_namespace_shape" {
  command = plan
  variables {
    eventhub_namespace_name = "bad_name"
  }
  expect_failures = [var.eventhub_namespace_name]
}

run "reject_scenario" {
  command = plan
  variables {
    scenario_id = "C01"
  }
  expect_failures = [var.scenario_id]
}

run "reject_run" {
  command = plan
  variables {
    run_id = ""
  }
  expect_failures = [var.run_id]
}

run "reject_expiry" {
  command = plan
  variables {
    expires_at = "tomorrow"
  }
  expect_failures = [var.expires_at]
}

run "probe_v2_roles_false" {
  command = plan
  variables {
    probe_namespace_data_roles = false
  }
  assert {
    condition = jsonencode(output.probe_v2_role_scope.assignments) == jsonencode({
      probe_events_receiver              = { service_account = "probe", role_definition_name = "Azure Event Hubs Data Receiver", scope = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events", principal_id = "00000000-0000-0000-0000-000000000013" }
      probe_single_action_contributor    = { service_account = "probe", role_definition_name = "Storage Blob Data Contributor", scope = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/single-action", principal_id = "00000000-0000-0000-0000-000000000013" }
      verifier_single_action_reader      = { service_account = "verifier", role_definition_name = "Storage Blob Data Reader", scope = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/single-action", principal_id = "00000000-0000-0000-0000-000000000006" }
      loadgen_events_sender              = { service_account = "loadgen", role_definition_name = "Azure Event Hubs Data Sender", scope = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events", principal_id = "00000000-0000-0000-0000-000000000004" }
      loadgen_ledger_contributor         = { service_account = "loadgen", role_definition_name = "Storage Blob Data Contributor", scope = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/ledger", principal_id = "00000000-0000-0000-0000-000000000004" }
      enrichment_events_receiver         = { service_account = "enrichment-consumer", role_definition_name = "Azure Event Hubs Data Receiver", scope = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events", principal_id = "00000000-0000-0000-0000-000000000005" }
      enrichment_state_sender            = { service_account = "enrichment-consumer", role_definition_name = "Azure Event Hubs Data Sender", scope = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-state", principal_id = "00000000-0000-0000-0000-000000000005" }
      enrichment_dlq_sender              = { service_account = "enrichment-consumer", role_definition_name = "Azure Event Hubs Data Sender", scope = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events-dlq", principal_id = "00000000-0000-0000-0000-000000000005" }
      enrichment_claim_check_contributor = { service_account = "enrichment-consumer", role_definition_name = "Storage Blob Data Contributor", scope = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/claim-check", principal_id = "00000000-0000-0000-0000-000000000005" }
      verifier_events_receiver           = { service_account = "verifier", role_definition_name = "Azure Event Hubs Data Receiver", scope = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events", principal_id = "00000000-0000-0000-0000-000000000006" }
      verifier_ledger_reader             = { service_account = "verifier", role_definition_name = "Storage Blob Data Reader", scope = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/ledger", principal_id = "00000000-0000-0000-0000-000000000006" }
      verifier_results_contributor       = { service_account = "verifier", role_definition_name = "Storage Blob Data Contributor", scope = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/verifier", principal_id = "00000000-0000-0000-0000-000000000006" }
    })
    error_message = "Disabled V2 roles must preserve the complete twelve-entry applied role map."
  }
  assert {
    condition = output.probe_v2_role_scope.probe_namespace_data_roles == false && output.probe_v2_role_scope.namespace_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1" && jsonencode(output.probe_v2_role_scope.topics) == jsonencode({
      document-events     = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events"
      document-state      = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-state"
      document-events-dlq = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1/eventhubs/document-events-dlq"
      }) && jsonencode(output.probe_v2_role_scope.accounts) == jsonencode({
      loadgen             = { client_id = "00000000-0000-0000-0000-000000000001", principal_id = "00000000-0000-0000-0000-000000000004" }
      probe               = { client_id = "00000000-0000-0000-0000-000000000012", principal_id = "00000000-0000-0000-0000-000000000013" }
      enrichment-consumer = { client_id = "00000000-0000-0000-0000-000000000002", principal_id = "00000000-0000-0000-0000-000000000005" }
      verifier            = { client_id = "00000000-0000-0000-0000-000000000003", principal_id = "00000000-0000-0000-0000-000000000006" }
    })
    error_message = "Private scope evidence must bind the namespace, all target hubs and all four persistent identities."
  }
}

run "probe_v2_roles_true" {
  command = plan
  variables {
    probe_namespace_data_roles = true
  }
  assert {
    condition = jsonencode(output.probe_v2_role_scope.assignments) == jsonencode(merge(run.probe_v2_roles_false.probe_v2_role_scope.assignments, {
      probe_namespace_sender   = { service_account = "probe", role_definition_name = "Azure Event Hubs Data Sender", scope = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1", principal_id = "00000000-0000-0000-0000-000000000013" }
      probe_namespace_receiver = { service_account = "probe", role_definition_name = "Azure Event Hubs Data Receiver", scope = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1", principal_id = "00000000-0000-0000-0000-000000000013" }
    })) && output.probe_v2_role_scope.probe_namespace_data_roles == true
    error_message = "Enabled V2 roles must add only probe namespace Sender and Receiver, preserving every existing grant."
  }
  assert {
    condition     = jsonencode(output.probe_v2_role_scope.accounts) == jsonencode(run.probe_v2_roles_false.probe_v2_role_scope.accounts) && jsonencode(output.probe_v2_role_scope.topics) == jsonencode(run.probe_v2_roles_false.probe_v2_role_scope.topics) && output.probe_v2_role_scope.namespace_id == run.probe_v2_roles_false.probe_v2_role_scope.namespace_id
    error_message = "The namespace-role switch must not alter any identity or target."
  }
}

run "archive_smoke_source" {
  command = plan
  variables {
    archive_smoke_enabled = true
  }
  override_data {
    target          = data.terraform_remote_state.persistent
    override_during = plan
    values = {
      "outputs" = {
        "workflow_principal_id"      = "00000000-0000-0000-0000-000000000009"
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
  override_data {
    target          = module.storage.module.containers["archive_smoke_source"].module.role_assignments.module.interfaces.data.azapi_resource_list.role_definitions[0]
    override_during = plan
    values          = { output = { results = [{ id = "/subscriptions/00000000-0000-0000-0000-000000000000/providers/Microsoft.Authorization/roleDefinitions/00000000-0000-0000-0000-000000000033", role_name = "Storage Blob Data Contributor" }] } }
  }
  override_resource {
    target          = module.storage.module.containers["archive_smoke_source"].azapi_resource.this
    override_during = plan
    values          = { id = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/archive-smoke-source", output = {} }
  }
  assert {
    condition     = jsonencode(local.storage_config.containers.archive_smoke_source) == jsonencode({ name = "archive-smoke-source", public_access = "None", role_assignments = { workflow = { role_definition_id_or_name = "Storage Blob Data Contributor", principal_id = "00000000-0000-0000-0000-000000000009", principal_type = "ServicePrincipal", skip_service_principal_aad_check = true } } }) && length(local.storage_config.containers) == 5 && length(local.storage_config.role_assignments) == 0 && output.blob.containers.archive_smoke_source == "archive-smoke-source" && length(output.blob.containers) == 5
    error_message = "Smoke adds exactly one nonpublic source container and its output without account grants."
  }
  assert {
    condition     = module.storage.containers.archive_smoke_source.id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/archive-smoke-source" && jsonencode(output.probe_v2_role_scope.assignments) == jsonencode(run.foundation.probe_v2_role_scope.assignments)
    error_message = "Smoke grant is scoped to the source container and preserves workload roles."
  }
}

run "default_active_contract" {
  command = plan
  assert {
    condition = jsonencode(nonsensitive(output.configuration_lifecycle)) == jsonencode({
      eventhubs_enabled                 = true
      storage_account_resource_id       = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1"
      blob_private_endpoint_resource_id = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Network/privateEndpoints/pe-blob-offline1"
      container_resource_ids = {
        ledger        = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/ledger"
        verifier      = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/verifier"
        claim_check   = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/claim-check"
        single_action = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/single-action"
      }
      workspace_resource_id                  = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.OperationalInsights/workspaces/law-parchwell-offline1"
      blob_diagnostic_target_resource_id     = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default"
      namespace_resource_id                  = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.EventHub/namespaces/eh-parchwell-offline1"
      eventhubs_private_endpoint_resource_id = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Network/privateEndpoints/pe-eventhubs-offline1"
    }) && local.names.eventhubs_private_endpoint == "pe-eventhubs-offline1"
    error_message = "Active lifecycle output must expose exactly the intended private resource identities and default endpoint."
  }
}

run "namespace_off_retains_storage" {
  command = plan
  variables {
    eventhubs_enabled = false
  }
  assert {
    condition     = output.eventhubs == null && output.probe_v2_role_scope == null && output.private_endpoint_ips.eventhubs == null && output.private_endpoint_ips.blob == "10.240.4.5" && length(module.eventhubs) == 0 && length(azurerm_eventhub.state) == 0 && length(azurerm_monitor_diagnostic_setting.eventhubs) == 0
    error_message = "Off mode must remove namespace dependencies while preserving the Blob endpoint."
  }
  assert {
    condition     = nonsensitive(output.configuration_lifecycle.eventhubs_enabled) == false && nonsensitive(output.configuration_lifecycle.namespace_resource_id) == null && nonsensitive(output.configuration_lifecycle.eventhubs_private_endpoint_resource_id) == null && nonsensitive(output.configuration_lifecycle.storage_account_resource_id) == nonsensitive(run.foundation.configuration_lifecycle.storage_account_resource_id) && nonsensitive(output.configuration_lifecycle.blob_private_endpoint_resource_id) == nonsensitive(run.foundation.configuration_lifecycle.blob_private_endpoint_resource_id) && jsonencode(nonsensitive(output.configuration_lifecycle.container_resource_ids)) == jsonencode(nonsensitive(run.foundation.configuration_lifecycle.container_resource_ids)) && nonsensitive(output.configuration_lifecycle.blob_diagnostic_target_resource_id) == nonsensitive(run.foundation.configuration_lifecycle.blob_diagnostic_target_resource_id) && length(azurerm_role_assignment.workload) == 6 && alltrue([for role in azurerm_role_assignment.workload : startswith(role.role_definition_name, "Storage Blob Data ")])
    error_message = "Off mode must preserve shared identities, diagnostics target and Blob-scoped roles."
  }
}

run "diagnostic_destinations" {
  command = plan
  assert {
    condition     = local.storage_blob_diagnostics.law.name == "diag-blob-offline1" && local.storage_blob_diagnostics.law.workspace_resource_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.OperationalInsights/workspaces/law-parchwell-offline1" && local.storage_blob_diagnostics.law.log_analytics_destination_type == "Dedicated" && toset([for item in local.storage_blob_diagnostics.law.logs : item.category]) == toset(["StorageRead", "StorageWrite", "StorageDelete"]) && toset([for item in local.storage_blob_diagnostics.law.metrics : item.category]) == toset(["Transaction"]) && nonsensitive(output.configuration_lifecycle.blob_diagnostic_target_resource_id) == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default"
    error_message = "Blob diagnostics must target the Blob service with exact categories and existing workspace."
  }
}

run "private_endpoint_override" {
  command = plan
  variables {
    eventhub_namespace_name        = "eh-new"
    eventhub_private_endpoint_name = "pe-eh-new"
  }
  assert {
    condition     = local.names.eventhubs_private_endpoint == "pe-eh-new" && local.eventhubs_config.private_endpoints.eventhubs.name == "pe-eh-new"
    error_message = "Explicit endpoint name must follow the current namespace."
  }
}

run "reject_private_endpoint_override" {
  command = plan
  variables {
    eventhub_namespace_name        = "eh-new"
    eventhub_private_endpoint_name = "pe-other"
  }
  expect_failures = [var.eventhub_private_endpoint_name]
}

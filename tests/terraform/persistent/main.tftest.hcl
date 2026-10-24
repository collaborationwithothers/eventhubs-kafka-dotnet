# Synthetic fixtures only. These plans prove configuration, not Azure readiness.

mock_provider "azurerm" {
  override_during = plan
  mock_resource "azurerm_role_assignment" {
    defaults = { id = "/subscriptions/00000000-0000-0000-0000-000000000000/providers/Microsoft.Authorization/roleAssignments/00000000-0000-0000-0000-000000000030" }
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
  naming_suffix                    = "offline1"
  location                         = "uksouth"
  runner_vnet_id                   = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-synthetic-runner/providers/Microsoft.Network/virtualNetworks/vnet-synthetic-runner"
  spoke_cidr                       = "10.240.0.0/16"
  aks_subnet_cidr                  = "10.240.0.0/22"
  private_endpoint_subnet_cidr     = "10.240.4.0/24"
  platform_dns_resource_group_name = "rg-platform-privatedns"
  create_private_dns_zones         = false
  workflow_principal_id            = "00000000-0000-0000-0000-000000000009"
}

override_resource {
  target          = azurerm_resource_group.persistent
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist" }
}

override_resource {
  target          = module.spoke.azapi_resource.vnet
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1", "output" = {} }
}

override_resource {
  target          = module.spoke.module.subnet["aks"].azapi_resource.subnet[0]
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/subnets/snet-aks", "output" = {} }
}

override_resource {
  target          = module.spoke.module.subnet["private_endpoints"].azapi_resource.subnet[0]
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/subnets/snet-private-endpoints", "output" = {} }
}

override_resource {
  target          = module.spoke.module.peering["runner"].azapi_resource.this[0]
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/virtualNetworkPeerings/peer-parchwell-to-runner", "output" = {} }
}

override_resource {
  target          = module.spoke.module.peering["runner"].azapi_resource.reverse[0]
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-synthetic-runner/providers/Microsoft.Network/virtualNetworks/vnet-synthetic-runner/virtualNetworkPeerings/peer-runner-to-parchwell-offline1", "output" = {} }
}

override_data {
  target          = data.azurerm_private_dns_zone.platform["eventhubs"]
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net" }
}

override_resource {
  target          = module.spoke_dns_links["eventhubs"].azapi_resource.private_dns_zone_network_link
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net/virtualNetworkLinks/link-parchwell-offline1", "output" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net/virtualNetworkLinks/link-parchwell-offline1", "name" = "link-parchwell-offline1", "type" = "Microsoft.Network/privateDnsZones/virtualNetworkLinks", "virtual_network" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1" }, "registration_enabled" = false } }
}

override_data {
  target          = data.azurerm_private_dns_zone.platform["blob"]
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net" }
}

override_resource {
  target          = module.spoke_dns_links["blob"].azapi_resource.private_dns_zone_network_link
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net/virtualNetworkLinks/link-parchwell-offline1", "output" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net/virtualNetworkLinks/link-parchwell-offline1", "name" = "link-parchwell-offline1", "type" = "Microsoft.Network/privateDnsZones/virtualNetworkLinks", "virtual_network" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1" }, "registration_enabled" = false } }
}

override_resource {
  target          = module.workload_identities["loadgen"].azurerm_user_assigned_identity.this
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-loadgen-offline1", "client_id" = "00000000-0000-0000-0000-000000000001", "principal_id" = "00000000-0000-0000-0000-000000000004" }
}

override_resource {
  target          = module.workload_identities["enrichment-consumer"].azurerm_user_assigned_identity.this
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-enrichment-consumer-offline1", "client_id" = "00000000-0000-0000-0000-000000000002", "principal_id" = "00000000-0000-0000-0000-000000000005" }
}

override_resource {
  target          = module.workload_identities["verifier"].azurerm_user_assigned_identity.this
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-verifier-offline1", "client_id" = "00000000-0000-0000-0000-000000000003", "principal_id" = "00000000-0000-0000-0000-000000000006" }
}

override_resource {
  target          = module.aks_identity.azurerm_user_assigned_identity.this
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-aks-offline1", "client_id" = "00000000-0000-0000-0000-000000000007", "principal_id" = "00000000-0000-0000-0000-000000000008" }
}

override_resource {
  target          = module.log_analytics.azurerm_log_analytics_workspace.this
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.OperationalInsights/workspaces/law-parchwell-offline1", "workspace_id" = "00000000-0000-0000-0000-000000000020" }
}

override_resource {
  target          = module.acr.azurerm_container_registry.this
  override_during = plan
  values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ContainerRegistry/registries/acrparchwelloffline1", "login_server" = "acrparchwelloffline1.azurecr.io" }
}

override_resource {
  target          = module.application_insights.azurerm_application_insights.this
  override_during = plan
  values = {
    "id"                  = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Insights/components/appi-parchwell-offline1"
    "app_id"              = "00000000-0000-0000-0000-000000000021"
    "instrumentation_key" = "00000000-0000-0000-0000-000000000022"
    "connection_string"   = "InstrumentationKey=00000000-0000-0000-0000-000000000022;IngestionEndpoint=https://synthetic.example.invalid/"
  }
}

override_data {
  target          = module.application_insights.data.azapi_client_config.this
  override_during = plan
  values = {
    subscription_id = "00000000-0000-0000-0000-000000000000"
  }
}

override_data {
  target          = module.application_insights.module.avm_interfaces.data.azapi_resource_list.role_definitions[0]
  override_during = plan
  values = {
    output = {
      results = [{
        id        = "/subscriptions/00000000-0000-0000-0000-000000000000/providers/Microsoft.Authorization/roleDefinitions/00000000-0000-0000-0000-000000000032"
        role_name = "Monitoring Metrics Publisher"
      }]
    }
  }
}

run "foundation" {
  command = plan

  assert {
    condition = jsonencode(local.application_insights_roles) == jsonencode({
      loadgen = {
        role_definition_id_or_name       = "Monitoring Metrics Publisher"
        principal_id                     = "00000000-0000-0000-0000-000000000004"
        principal_type                   = "ServicePrincipal"
        skip_service_principal_aad_check = true
      }
      verifier = {
        role_definition_id_or_name       = "Monitoring Metrics Publisher"
        principal_id                     = "00000000-0000-0000-0000-000000000006"
        principal_type                   = "ServicePrincipal"
        skip_service_principal_aad_check = true
      }
    })
    error_message = "Application Insights must receive only loadgen and verifier telemetry grants through its component-scoped module interface."
  }

  assert {
    condition     = toset(module.spoke.resource.body.properties.addressSpace.addressPrefixes) == toset(["10.240.0.0/16"]) && module.spoke.subnets.aks.resource.body.properties.addressPrefix == "10.240.0.0/22" && module.spoke.subnets.private_endpoints.resource.body.properties.addressPrefix == "10.240.4.0/24" && module.spoke.subnets.private_endpoints.resource.body.properties.privateEndpointNetworkPolicies == "Disabled"
    error_message = "Spoke and subnet bodies must match their exact synthetic address and endpoint policy contract."
  }

  assert {
    condition     = module.spoke.peerings.runner.resource_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/virtualNetworkPeerings/peer-parchwell-to-runner" && module.spoke.peerings.runner.reverse_resource_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-synthetic-runner/providers/Microsoft.Network/virtualNetworks/vnet-synthetic-runner/virtualNetworkPeerings/peer-runner-to-parchwell-offline1"
    error_message = "Both peering instances must expose their exact IDs."
  }

  assert {
    condition     = jsonencode(local.spoke_config) == jsonencode({ "name" = "vnet-parchwell-offline1", "location" = "uksouth", "parent_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist", "address_space" = ["10.240.0.0/16"], "enable_telemetry" = false, "tags" = { "repo" = "eventhubs-kafka-dotnet", "layer" = "persistent", "scenario" = "shared", "run-id" = "persistent", "expires-at" = "never" }, "subnets" = { "aks" = { "name" = "snet-aks", "address_prefix" = "10.240.0.0/22" }, "private_endpoints" = { "name" = "snet-private-endpoints", "address_prefix" = "10.240.4.0/24", "private_endpoint_network_policies" = "Disabled" } }, "peerings" = { "runner" = { "name" = "peer-parchwell-to-runner", "remote_virtual_network_resource_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-synthetic-runner/providers/Microsoft.Network/virtualNetworks/vnet-synthetic-runner", "create_reverse_peering" = true, "reverse_name" = "peer-runner-to-parchwell-offline1" } } })
    error_message = "Spoke module must receive the exact literal configuration, including its RG parent and reverse peering."
  }

  assert {
    condition     = jsonencode(output.spoke_vnet_id) == jsonencode("/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1")
    error_message = "spoke_vnet_id output must match the complete literal synthetic fixture."
  }

  assert {
    condition     = jsonencode(output.aks_subnet_id) == jsonencode("/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/subnets/snet-aks")
    error_message = "aks_subnet_id output must match the complete literal synthetic fixture."
  }

  assert {
    condition     = jsonencode(output.private_endpoint_subnet_id) == jsonencode("/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1/subnets/snet-private-endpoints")
    error_message = "private_endpoint_subnet_id output must match the complete literal synthetic fixture."
  }

  assert {
    condition     = jsonencode(output.private_dns_zone_ids) == jsonencode({ "eventhubs" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net", "blob" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net" })
    error_message = "private_dns_zone_ids output must match the complete literal synthetic fixture."
  }

  assert {
    condition     = jsonencode(output.log_analytics_workspace_id) == jsonencode("/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.OperationalInsights/workspaces/law-parchwell-offline1")
    error_message = "log_analytics_workspace_id output must match the complete literal synthetic fixture."
  }

  assert {
    condition     = jsonencode(output.workload_identities) == jsonencode({ "loadgen" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-loadgen-offline1", "client_id" = "00000000-0000-0000-0000-000000000001", "principal_id" = "00000000-0000-0000-0000-000000000004" }, "enrichment-consumer" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-enrichment-consumer-offline1", "client_id" = "00000000-0000-0000-0000-000000000002", "principal_id" = "00000000-0000-0000-0000-000000000005" }, "verifier" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-verifier-offline1", "client_id" = "00000000-0000-0000-0000-000000000003", "principal_id" = "00000000-0000-0000-0000-000000000006" } })
    error_message = "workload_identities output must match the complete literal synthetic fixture."
  }

  assert {
    condition     = jsonencode(output.aks_identity_id) == jsonencode("/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uami-aks-offline1")
    error_message = "aks_identity_id output must match the complete literal synthetic fixture."
  }

  assert {
    condition     = jsonencode(output.acr) == jsonencode({ "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ContainerRegistry/registries/acrparchwelloffline1", "name" = "acrparchwelloffline1", "login_server" = "acrparchwelloffline1.azurecr.io" })
    error_message = "acr output must match the complete literal synthetic fixture."
  }

  assert {
    condition     = length(module.owned_dns) == 0 && length(module.runner_dns_links) == 0 && length(data.azurerm_private_dns_zone.platform) == 2
    error_message = "Existing-zone mode must own neither zones nor runner links."
  }

  assert {
    condition     = jsonencode(module.spoke_dns_links["eventhubs"].resource) == jsonencode({ "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net/virtualNetworkLinks/link-parchwell-offline1", "name" = "link-parchwell-offline1", "type" = "Microsoft.Network/privateDnsZones/virtualNetworkLinks", "virtual_network" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1" }, "registration_enabled" = false })
    error_message = "eventhubs spoke link response must match its exact synthetic fixture."
  }

  assert {
    condition     = jsonencode(local.dns_link_config.spoke["eventhubs"]) == jsonencode({ "name" = "link-parchwell-offline1", "parent_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net", "virtual_network_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1", "registration_enabled" = false, "private_dns_zone_supports_private_link" = true, "resolution_policy" = "Default", "tags" = { "repo" = "eventhubs-kafka-dotnet", "layer" = "persistent", "scenario" = "shared", "run-id" = "persistent", "expires-at" = "never" } })
    error_message = "eventhubs spoke link must receive the exact selected zone and network configuration."
  }

  assert {
    condition     = jsonencode(module.spoke_dns_links["blob"].resource) == jsonencode({ "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net/virtualNetworkLinks/link-parchwell-offline1", "name" = "link-parchwell-offline1", "type" = "Microsoft.Network/privateDnsZones/virtualNetworkLinks", "virtual_network" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1" }, "registration_enabled" = false })
    error_message = "blob spoke link response must match its exact synthetic fixture."
  }

  assert {
    condition     = jsonencode(local.dns_link_config.spoke["blob"]) == jsonencode({ "name" = "link-parchwell-offline1", "parent_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-platform-privatedns/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net", "virtual_network_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1", "registration_enabled" = false, "private_dns_zone_supports_private_link" = true, "resolution_policy" = "Default", "tags" = { "repo" = "eventhubs-kafka-dotnet", "layer" = "persistent", "scenario" = "shared", "run-id" = "persistent", "expires-at" = "never" } })
    error_message = "blob spoke link must receive the exact selected zone and network configuration."
  }

  assert {
    condition     = azurerm_role_assignment.acr_push.scope == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.ContainerRegistry/registries/acrparchwelloffline1" && azurerm_role_assignment.acr_push.principal_id == "00000000-0000-0000-0000-000000000009" && azurerm_role_assignment.acr_push.role_definition_name == "AcrPush"
    error_message = "Only the configured workflow principal receives AcrPush on the actual registry."
  }

  assert {
    condition     = jsonencode(local.acr_config) == jsonencode({ "name" = "acrparchwelloffline1", "location" = "uksouth", "resource_group_name" = "rg-parchwell-persist", "tags" = { "repo" = "eventhubs-kafka-dotnet", "layer" = "persistent", "scenario" = "shared", "run-id" = "persistent", "expires-at" = "never" }, "enable_telemetry" = false, "sku" = "Standard", "public_network_access_enabled" = true, "admin_enabled" = false, "anonymous_pull_enabled" = false, "network_rule_set" = null, "zone_redundancy_enabled" = false, "role_assignment_mode" = "LegacyRegistryPermissions", "private_endpoints" = {} }) && length(module.acr.private_endpoints) == 0
    error_message = "Registry input contract must retain approved Entra-only public Standard policy and no private endpoint."
  }

  assert {
    condition     = module.log_analytics.resource.retention_in_days == 30 && module.log_analytics.resource.sku == "PerGB2018" && module.log_analytics.resource.internet_ingestion_enabled && module.log_analytics.resource.internet_query_enabled && !module.log_analytics.resource.local_authentication_enabled
    error_message = "Workspace must retain the selected retention, SKU and authentication configuration."
  }

  assert {
    condition     = jsonencode(local.tags) == jsonencode({ "repo" = "eventhubs-kafka-dotnet", "layer" = "persistent", "scenario" = "shared", "run-id" = "persistent", "expires-at" = "never" }) && jsonencode(azurerm_resource_group.persistent.tags) == jsonencode(local.tags) && jsonencode(module.spoke.resource.tags) == jsonencode(local.tags) && jsonencode(module.log_analytics.resource.tags) == jsonencode(local.tags) && jsonencode(module.aks_identity.resource.tags) == jsonencode(local.tags) && alltrue([for identity in module.workload_identities : jsonencode(identity.resource.tags) == jsonencode(local.tags)])
    error_message = "All exposed persistent resources must receive the complete shared tag map."
  }

  assert {
    condition     = module.application_insights.resource.workspace_id == "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.OperationalInsights/workspaces/law-parchwell-offline1" && module.application_insights.resource.application_type == "other" && module.application_insights.resource.internet_ingestion_enabled && module.application_insights.resource.internet_query_enabled && module.application_insights.resource.local_authentication_disabled && module.application_insights.resource.retention_in_days == 90 && module.application_insights.resource.resource_group_name == "rg-parchwell-persist" && module.application_insights.resource.name == "appi-parchwell-offline1" && jsonencode(module.application_insights.resource.tags) == jsonencode(local.tags)
    error_message = "Application Insights must retain its persistent parent/workspace, public Azure Monitor topology, disabled local authentication, 90-day component retention and shared tags."
  }

  assert {
    condition     = issensitive(output.application_insights_connection_string) && output.application_insights_connection_string == "InstrumentationKey=00000000-0000-0000-0000-000000000022;IngestionEndpoint=https://synthetic.example.invalid/"
    error_message = "Sensitive Application Insights output must preserve the exact synthetic connection string."
  }
}

run "standalone_dns" {
  command = plan
  variables {
    create_private_dns_zones = true
  }
  override_resource {
    target          = module.owned_dns["eventhubs"].azapi_resource.private_dns_zone
    override_during = plan
    values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net", "output" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net", "name" = "privatelink.servicebus.windows.net", "type" = "Microsoft.Network/privateDnsZones", "location" = "global", "tags" = { "repo" = "eventhubs-kafka-dotnet", "layer" = "persistent", "scenario" = "shared", "run-id" = "persistent", "expires-at" = "never" } } }
  }
  override_resource {
    target          = module.spoke_dns_links["eventhubs"].azapi_resource.private_dns_zone_network_link
    override_during = plan
    values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net/virtualNetworkLinks/link-parchwell-offline1", "output" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net/virtualNetworkLinks/link-parchwell-offline1", "name" = "link-parchwell-offline1", "type" = "Microsoft.Network/privateDnsZones/virtualNetworkLinks", "virtual_network" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1" }, "registration_enabled" = false } }
  }
  override_resource {
    target          = module.runner_dns_links["eventhubs"].azapi_resource.private_dns_zone_network_link
    override_during = plan
    values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net/virtualNetworkLinks/link-runner-offline1", "output" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net/virtualNetworkLinks/link-runner-offline1", "name" = "link-runner-offline1", "type" = "Microsoft.Network/privateDnsZones/virtualNetworkLinks", "virtual_network" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-synthetic-runner/providers/Microsoft.Network/virtualNetworks/vnet-synthetic-runner" }, "registration_enabled" = false } }
  }
  override_resource {
    target          = module.owned_dns["blob"].azapi_resource.private_dns_zone
    override_during = plan
    values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net", "output" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net", "name" = "privatelink.blob.core.windows.net", "type" = "Microsoft.Network/privateDnsZones", "location" = "global", "tags" = { "repo" = "eventhubs-kafka-dotnet", "layer" = "persistent", "scenario" = "shared", "run-id" = "persistent", "expires-at" = "never" } } }
  }
  override_resource {
    target          = module.spoke_dns_links["blob"].azapi_resource.private_dns_zone_network_link
    override_during = plan
    values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net/virtualNetworkLinks/link-parchwell-offline1", "output" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net/virtualNetworkLinks/link-parchwell-offline1", "name" = "link-parchwell-offline1", "type" = "Microsoft.Network/privateDnsZones/virtualNetworkLinks", "virtual_network" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1" }, "registration_enabled" = false } }
  }
  override_resource {
    target          = module.runner_dns_links["blob"].azapi_resource.private_dns_zone_network_link
    override_during = plan
    values          = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net/virtualNetworkLinks/link-runner-offline1", "output" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net/virtualNetworkLinks/link-runner-offline1", "name" = "link-runner-offline1", "type" = "Microsoft.Network/privateDnsZones/virtualNetworkLinks", "virtual_network" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-synthetic-runner/providers/Microsoft.Network/virtualNetworks/vnet-synthetic-runner" }, "registration_enabled" = false } }
  }
  assert {
    condition     = length(module.owned_dns) == 2 && length(module.runner_dns_links) == 2 && length(module.spoke_dns_links) == 2 && length(data.azurerm_private_dns_zone.platform) == 0
    error_message = "Standalone mode must own exactly two zones and both pairs of links, with no external-zone lookup."
  }
  assert {
    condition     = jsonencode(output.private_dns_zone_ids) == jsonencode({ "eventhubs" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net", "blob" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net" })
    error_message = "Standalone zone outputs must use owned resource IDs."
  }
  assert {
    condition     = jsonencode(module.owned_dns["eventhubs"].resource) == jsonencode({ "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net", "name" = "privatelink.servicebus.windows.net", "type" = "Microsoft.Network/privateDnsZones", "location" = "global", "tags" = { "repo" = "eventhubs-kafka-dotnet", "layer" = "persistent", "scenario" = "shared", "run-id" = "persistent", "expires-at" = "never" } })
    error_message = "Owned eventhubs zone response must match the exact synthetic name, ID, type, location and tags."
  }
  assert {
    condition     = jsonencode(module.spoke_dns_links["eventhubs"].resource) == jsonencode({ "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net/virtualNetworkLinks/link-parchwell-offline1", "name" = "link-parchwell-offline1", "type" = "Microsoft.Network/privateDnsZones/virtualNetworkLinks", "virtual_network" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1" }, "registration_enabled" = false })
    error_message = "Standalone eventhubs spoke link response must match its full synthetic fixture."
  }
  assert {
    condition     = jsonencode(local.dns_link_config.spoke["eventhubs"]) == jsonencode({ "name" = "link-parchwell-offline1", "parent_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net", "virtual_network_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1", "registration_enabled" = false, "private_dns_zone_supports_private_link" = true, "resolution_policy" = "Default", "tags" = { "repo" = "eventhubs-kafka-dotnet", "layer" = "persistent", "scenario" = "shared", "run-id" = "persistent", "expires-at" = "never" } })
    error_message = "Standalone eventhubs spoke link must receive the exact parent, network, registration, resolution and tags."
  }
  assert {
    condition     = jsonencode(module.runner_dns_links["eventhubs"].resource) == jsonencode({ "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net/virtualNetworkLinks/link-runner-offline1", "name" = "link-runner-offline1", "type" = "Microsoft.Network/privateDnsZones/virtualNetworkLinks", "virtual_network" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-synthetic-runner/providers/Microsoft.Network/virtualNetworks/vnet-synthetic-runner" }, "registration_enabled" = false })
    error_message = "Standalone eventhubs runner link response must match its full synthetic fixture."
  }
  assert {
    condition     = jsonencode(local.dns_link_config.runner["eventhubs"]) == jsonencode({ "name" = "link-runner-offline1", "parent_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.servicebus.windows.net", "virtual_network_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-synthetic-runner/providers/Microsoft.Network/virtualNetworks/vnet-synthetic-runner", "registration_enabled" = false, "private_dns_zone_supports_private_link" = true, "resolution_policy" = "Default", "tags" = { "repo" = "eventhubs-kafka-dotnet", "layer" = "persistent", "scenario" = "shared", "run-id" = "persistent", "expires-at" = "never" } })
    error_message = "Standalone eventhubs runner link must receive the exact parent, network, registration, resolution and tags."
  }
  assert {
    condition     = jsonencode(module.owned_dns["blob"].resource) == jsonencode({ "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net", "name" = "privatelink.blob.core.windows.net", "type" = "Microsoft.Network/privateDnsZones", "location" = "global", "tags" = { "repo" = "eventhubs-kafka-dotnet", "layer" = "persistent", "scenario" = "shared", "run-id" = "persistent", "expires-at" = "never" } })
    error_message = "Owned blob zone response must match the exact synthetic name, ID, type, location and tags."
  }
  assert {
    condition     = jsonencode(module.spoke_dns_links["blob"].resource) == jsonencode({ "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net/virtualNetworkLinks/link-parchwell-offline1", "name" = "link-parchwell-offline1", "type" = "Microsoft.Network/privateDnsZones/virtualNetworkLinks", "virtual_network" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1" }, "registration_enabled" = false })
    error_message = "Standalone blob spoke link response must match its full synthetic fixture."
  }
  assert {
    condition     = jsonencode(local.dns_link_config.spoke["blob"]) == jsonencode({ "name" = "link-parchwell-offline1", "parent_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net", "virtual_network_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/virtualNetworks/vnet-parchwell-offline1", "registration_enabled" = false, "private_dns_zone_supports_private_link" = true, "resolution_policy" = "Default", "tags" = { "repo" = "eventhubs-kafka-dotnet", "layer" = "persistent", "scenario" = "shared", "run-id" = "persistent", "expires-at" = "never" } })
    error_message = "Standalone blob spoke link must receive the exact parent, network, registration, resolution and tags."
  }
  assert {
    condition     = jsonencode(module.runner_dns_links["blob"].resource) == jsonencode({ "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net/virtualNetworkLinks/link-runner-offline1", "name" = "link-runner-offline1", "type" = "Microsoft.Network/privateDnsZones/virtualNetworkLinks", "virtual_network" = { "id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-synthetic-runner/providers/Microsoft.Network/virtualNetworks/vnet-synthetic-runner" }, "registration_enabled" = false })
    error_message = "Standalone blob runner link response must match its full synthetic fixture."
  }
  assert {
    condition     = jsonencode(local.dns_link_config.runner["blob"]) == jsonencode({ "name" = "link-runner-offline1", "parent_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-persist/providers/Microsoft.Network/privateDnsZones/privatelink.blob.core.windows.net", "virtual_network_id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-synthetic-runner/providers/Microsoft.Network/virtualNetworks/vnet-synthetic-runner", "registration_enabled" = false, "private_dns_zone_supports_private_link" = true, "resolution_policy" = "Default", "tags" = { "repo" = "eventhubs-kafka-dotnet", "layer" = "persistent", "scenario" = "shared", "run-id" = "persistent", "expires-at" = "never" } })
    error_message = "Standalone blob runner link must receive the exact parent, network, registration, resolution and tags."
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

run "reject_runner_id" {
  command = plan
  variables {
    runner_vnet_id = "not-an-id"
  }
  expect_failures = [var.runner_vnet_id]
}

run "reject_spoke_cidr" {
  command = plan
  variables {
    spoke_cidr = "not-cidr"
  }
  expect_failures = [var.spoke_cidr]
}

run "reject_aks_subnet_cidr" {
  command = plan
  variables {
    aks_subnet_cidr = "not-cidr"
  }
  expect_failures = [var.aks_subnet_cidr]
}

run "reject_pe_subnet_cidr" {
  command = plan
  variables {
    private_endpoint_subnet_cidr = "not-cidr"
  }
  expect_failures = [var.private_endpoint_subnet_cidr]
}

run "reject_dns_rg" {
  command = plan
  variables {
    platform_dns_resource_group_name = ""
  }
  expect_failures = [var.platform_dns_resource_group_name]
}

run "reject_workflow_principal" {
  command = plan
  variables {
    workflow_principal_id = "not-a-guid"
  }
  expect_failures = [var.workflow_principal_id]
}

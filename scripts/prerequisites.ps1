<#
.SYNOPSIS
    Creates the platform prerequisites that live outside this repo's Terraform (ADR-010).

.DESCRIPTION
    Creates, if absent:
      - resource group rg-platform-privatedns
      - private DNS zones privatelink.servicebus.windows.net and privatelink.blob.core.windows.net
      - a virtual network link from each zone to the existing runner VNet
      - a CanNotDelete lock on each zone's SOA record set, which blocks deleting the zone
        while leaving A records free to be created and deleted by each session
      - optionally, two custom roles assigned to the identity that runs this repo's Terraform:
          "Private DNS Record and Link Manager" on the DNS resource group (cannot delete zones)
          "Runner VNet Peering Manager" on the runner VNet (read the VNet, manage peerings only)

    Safe to re-run: every step checks for the resource first.

    Status: run by Hari on 2026-10-11 against the live subscription. Re-runs are safe.

.NOTES
    Requires Az.Accounts, Az.Resources and Az.PrivateDns.
    Creating the custom role and its assignment needs Owner or User Access Administrator.
    Record-set locks can only be created with Azure PowerShell, not the Azure CLI,
    which is why this script is PowerShell.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string] $SubscriptionId,

    [Parameter(Mandatory)]
    [string] $RunnerVnetResourceId,

    [string] $ResourceGroupName = 'rg-platform-privatedns',

    [string] $Location = 'uksouth',

    # Object ID of the identity that runs this repo's Terraform. Omit to skip the role step.
    [string] $DeployerPrincipalId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$zones    = @('privatelink.servicebus.windows.net', 'privatelink.blob.core.windows.net')
$linkName = 'link-runner-vnet'
$lockName = 'protect-zone-via-soa'
$roleName = 'Private DNS Record and Link Manager'
$peeringRoleName = 'Runner VNet Peering Manager'

Set-AzContext -Subscription $SubscriptionId | Out-Null

if (-not (Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue)) {
    if ($PSCmdlet.ShouldProcess($ResourceGroupName, 'Create resource group')) {
        New-AzResourceGroup -Name $ResourceGroupName -Location $Location | Out-Null
    }
}

foreach ($zone in $zones) {
    if (-not (Get-AzPrivateDnsZone -ResourceGroupName $ResourceGroupName -Name $zone -ErrorAction SilentlyContinue)) {
        if ($PSCmdlet.ShouldProcess($zone, 'Create private DNS zone')) {
            New-AzPrivateDnsZone -ResourceGroupName $ResourceGroupName -Name $zone | Out-Null
        }
    }

    if (-not (Get-AzPrivateDnsVirtualNetworkLink -ResourceGroupName $ResourceGroupName -ZoneName $zone -Name $linkName -ErrorAction SilentlyContinue)) {
        if ($PSCmdlet.ShouldProcess("$zone -> runner VNet", 'Create virtual network link')) {
            New-AzPrivateDnsVirtualNetworkLink -ResourceGroupName $ResourceGroupName -ZoneName $zone `
                -Name $linkName -VirtualNetworkId $RunnerVnetResourceId | Out-Null
        }
    }

    # The SOA record set is named '@'. Locking it blocks zone deletion only (ADR-010, S16).
    $soaName = "$zone/@"
    $soaType = 'Microsoft.Network/privateDnsZones/SOA'
    $existingLock = Get-AzResourceLock -ResourceGroupName $ResourceGroupName -ResourceName $soaName `
        -ResourceType $soaType -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq $lockName }
    if (-not $existingLock) {
        if ($PSCmdlet.ShouldProcess($soaName, 'Create CanNotDelete lock on SOA record set')) {
            New-AzResourceLock -LockLevel CanNotDelete -LockName $lockName -ResourceName $soaName `
                -ResourceType $soaType -ResourceGroupName $ResourceGroupName -Force | Out-Null
        }
    }
}

if ($DeployerPrincipalId) {
    $scope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName"

    if (-not (Get-AzRoleDefinition -Name $roleName -ErrorAction SilentlyContinue)) {
        $role = [Microsoft.Azure.Commands.Resources.Models.Authorization.PSRoleDefinition]::new()
        $role.Name        = $roleName
        $role.Description = 'Manage A records and VNet links in private DNS zones. Cannot delete zones (ADR-010).'
        $role.IsCustom    = $true
        # Action list to confirm on first apply (blueprint open item 6).
        $role.Actions = @(
            'Microsoft.Network/privateDnsZones/read',
            'Microsoft.Network/privateDnsZones/join/action',
            'Microsoft.Network/privateDnsZones/SOA/read',
            'Microsoft.Network/privateDnsZones/A/read',
            'Microsoft.Network/privateDnsZones/A/write',
            'Microsoft.Network/privateDnsZones/A/delete',
            'Microsoft.Network/privateDnsZones/virtualNetworkLinks/read',
            'Microsoft.Network/privateDnsZones/virtualNetworkLinks/write',
            'Microsoft.Network/privateDnsZones/virtualNetworkLinks/delete',
            'Microsoft.Network/privateDnsOperationResults/read',
            'Microsoft.Network/privateDnsOperationStatuses/read'
        )
        $role.AssignableScopes = @($scope)
        if ($PSCmdlet.ShouldProcess($roleName, 'Create custom role')) {
            New-AzRoleDefinition -Role $role | Out-Null
        }
    }

    $assigned = Get-AzRoleAssignment -ObjectId $DeployerPrincipalId -RoleDefinitionName $roleName -Scope $scope -ErrorAction SilentlyContinue
    if (-not $assigned) {
        if ($PSCmdlet.ShouldProcess($DeployerPrincipalId, "Assign '$roleName' at $scope")) {
            New-AzRoleAssignment -ObjectId $DeployerPrincipalId -RoleDefinitionName $roleName -Scope $scope | Out-Null
        }
    }
}

if ($DeployerPrincipalId) {
    # Lets this repo's Terraform create both sides of the spoke-to-runner peering (ADR-010)
    # without Network Contributor on the runner VNet. Action list to confirm on first apply.
    if (-not (Get-AzRoleDefinition -Name $peeringRoleName -ErrorAction SilentlyContinue)) {
        $peeringRole = [Microsoft.Azure.Commands.Resources.Models.Authorization.PSRoleDefinition]::new()
        $peeringRole.Name        = $peeringRoleName
        $peeringRole.Description = 'Read the runner VNet and manage its peerings only (ADR-010).'
        $peeringRole.IsCustom    = $true
        $peeringRole.Actions = @(
            'Microsoft.Network/virtualNetworks/read',
            'Microsoft.Network/virtualNetworks/peer/action',
            'Microsoft.Network/virtualNetworks/virtualNetworkPeerings/read',
            'Microsoft.Network/virtualNetworks/virtualNetworkPeerings/write',
            'Microsoft.Network/virtualNetworks/virtualNetworkPeerings/delete'
        )
        $peeringRole.AssignableScopes = @($RunnerVnetResourceId)
        if ($PSCmdlet.ShouldProcess($peeringRoleName, 'Create custom role')) {
            New-AzRoleDefinition -Role $peeringRole | Out-Null
        }
    }

    $peeringAssigned = Get-AzRoleAssignment -ObjectId $DeployerPrincipalId -RoleDefinitionName $peeringRoleName `
        -Scope $RunnerVnetResourceId -ErrorAction SilentlyContinue
    if (-not $peeringAssigned) {
        if ($PSCmdlet.ShouldProcess($DeployerPrincipalId, "Assign '$peeringRoleName' on the runner VNet")) {
            New-AzRoleAssignment -ObjectId $DeployerPrincipalId -RoleDefinitionName $peeringRoleName `
                -Scope $RunnerVnetResourceId | Out-Null
        }
    }
}

Write-Output 'Prerequisites in place.'

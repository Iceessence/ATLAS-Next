param([Parameter(Mandatory)][string]$Name)
$ErrorActionPreference='Stop'
# Read-only preflight. This command cannot attest isolation or enable execution.
if(!(Get-Command Get-VM -ErrorAction SilentlyContinue)){throw 'Hyper-V management cmdlets unavailable'}
$vm=Get-VM -Name $Name
$hostSettings=Get-VMHost
$adapters=@(Get-VMNetworkAdapter -VM $vm)
$disks=@(Get-VMHardDiskDrive -VM $vm)
[pscustomobject]@{
    vm=$vm.Name
    state="$($vm.State)"
    networkAdapterCount=$adapters.Count
    enhancedSessionHostPolicy=$hostSettings.EnableEnhancedSessionMode
    diskCount=$disks.Count
    resourceMemoryBytes=$vm.MemoryStartup
    executionAuthorized=$false
    verdict='Configuration observation only. Independent effective-isolation review required.'
} | ConvertTo-Json

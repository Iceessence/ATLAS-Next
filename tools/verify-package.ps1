param([Parameter(Mandatory)][string]$Zip)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$root=Join-Path ([IO.Path]::GetTempPath()) ("atlas-next-package-"+[guid]::NewGuid().ToString("N"))
$payload=Join-Path $root "extracted"
[IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $Zip).Path,$payload)
$inventory=Get-Content -LiteralPath (Join-Path $payload 'inventory.json') -Raw | ConvertFrom-Json
foreach($entry in $inventory){
    $path=Join-Path $payload $entry.path
    if((Get-FileHash -LiteralPath $path).Hash -ne $entry.sha256){throw "Bad packaged file $($entry.path)"}
}
$env:ATLAS_NEXT_PROFILE=Join-Path $root 'profile'
function Launch {
    $launcher=Join-Path $payload 'ATLAS Next.cmd'
    Start-Process -FilePath $launcher -WindowStyle Hidden
    $limit=[DateTime]::UtcNow.AddSeconds(15)
    do {
        $proc=Get-Process AtlasNext -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq (Join-Path $payload 'app\AtlasNext.exe') -and $_.MainWindowHandle -ne 0} | Select-Object -First 1
        if($proc){return $proc}
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $limit)
    throw 'Packaged window unavailable'
}
function Control($window,$id){
    $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$id)
    $deadline=[DateTime]::UtcNow.AddSeconds(5)
    do {
        $elem=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
        if($elem){return $elem}
        Start-Sleep -Milliseconds 75
    } while ([DateTime]::UtcNow -lt $deadline)
    if(!$elem){throw "Missing control $id"}
    return $elem
}
function Invoke-Control($window,$id){
    $el=Control $window $id
    $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
function Wait-State($window,$wanted){
    $limit=[DateTime]::UtcNow.AddSeconds(8)
    do {
        if((Control $window 'Status').Current.Name -eq $wanted){return}
        Start-Sleep -Milliseconds 100
    }while([DateTime]::UtcNow -lt $limit)
    throw "Expected task state $wanted"
}
$proc=Launch
try {
    $module=$proc.Modules | Where-Object ModuleName -eq 'coreclr.dll'
    if(!$module.FileName.StartsWith((Join-Path $payload 'runtime\'),[StringComparison]::OrdinalIgnoreCase)){throw 'External runtime loaded'}
    $window=[System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
    Invoke-Control $window 'Search'
    Wait-State $window 'review'
    Invoke-Control $window 'Verify'
    Wait-State $window 'completed'
    Invoke-Control $window 'Export'
    $limit=[DateTime]::UtcNow.AddSeconds(8)
    do {
        $file=Get-ChildItem (Join-Path $env:ATLAS_NEXT_PROFILE 'exports') -Filter '*.json' -ErrorAction SilentlyContinue|Select-Object -First 1
        if($file){break}
        Start-Sleep -Milliseconds 100
    }while([DateTime]::UtcNow -lt $limit)
    $receipt=Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    if($receipt.result -notlike '*30 days*' -or $receipt.proposal.qualification -ne 'shadow'){throw 'Invalid packaged receipt'}
    [void]$proc.CloseMainWindow()
    if(!$proc.WaitForExit(5000)){throw 'Shutdown timeout'}
    $proc=Launch
    $window=[System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
    Wait-State $window 'completed'
    Invoke-Control $window 'Search'
    Wait-State $window 'review'
    Invoke-Control $window 'Stop'
    Wait-State $window 'cancelled'
    # Terminate only the process returned by this test's private extracted launcher.
    Invoke-Control $window 'Search'
    Wait-State $window 'review'
    if($proc.Path -ne (Join-Path $payload 'app\AtlasNext.exe')){throw 'Unexpected process path'}
    $proc.Kill()
    if(!$proc.WaitForExit(5000)){throw 'Crash fixture shutdown timeout'}
    $proc=Launch
    $window=[System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
    Wait-State $window 'interrupted'
    "Package verified: $($inventory.Count) files; private runtime; real window; completion, export, relaunch, cancellation and forced process restart"
    "Evidence: $($file.FullName)"
} finally {
    if(!$proc.HasExited){[void]$proc.CloseMainWindow(); if(!$proc.WaitForExit(5000)){$proc.Kill()}}
}

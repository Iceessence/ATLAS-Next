param([Parameter(Mandatory)][string]$Zip)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
# Some Windows UIA providers expose native common-dialog controls as generic panes.
# Use their scoped HWNDs for the two native messages when managed patterns are absent.
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class AtlasTestDialog {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wparam, string text);
    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wparam, IntPtr lparam);
}
"@
$root=Join-Path ([IO.Path]::GetTempPath()) ("atlas-next-package-"+[guid]::NewGuid().ToString("N"))
$payload=Join-Path $root "extracted"
[IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $Zip).Path,$payload)
$inventoryBytes=[IO.File]::ReadAllBytes((Join-Path $payload 'inventory.json'))
if ($inventoryBytes[0] -ne 91) { throw 'Expected a BOM-free JSON inventory array' }
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
        $proc=Get-Process AtlasNext -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq (Join-Path $payload 'app\AtlasNext.exe')} | Select-Object -First 1
        if($proc){
            $automation=[System.Windows.Automation.AutomationElement]
            $condition=[System.Windows.Automation.AndCondition]::new(
                [System.Windows.Automation.PropertyCondition]::new($automation::ProcessIdProperty,[int]$proc.Id),
                [System.Windows.Automation.PropertyCondition]::new($automation::NameProperty,'ATLAS Next'),
                [System.Windows.Automation.PropertyCondition]::new($automation::ControlTypeProperty,[System.Windows.Automation.ControlType]::Window))
            $window=$automation::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,$condition)
            if($window){
                $ready=[System.Windows.Automation.PropertyCondition]::new($automation::AutomationIdProperty,'Status')
                if($window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$ready)){
                    return [pscustomobject]@{Process=$proc; Window=$window}
                }
            }
        }
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
function Import-Document($session,$name,$content){
    $path=Join-Path $root $name
    [IO.File]::WriteAllText($path,$content,[Text.UTF8Encoding]::new($false))
    Invoke-Control $session.Window 'Import'
    $automation=[System.Windows.Automation.AutomationElement]
    $condition=[System.Windows.Automation.AndCondition]::new(
        [System.Windows.Automation.PropertyCondition]::new($automation::ProcessIdProperty,[int]$session.Process.Id),
        [System.Windows.Automation.PropertyCondition]::new($automation::NameProperty,'Import local text'))
    $limit=[DateTime]::UtcNow.AddSeconds(10)
    do {
        $dialog=$session.Window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
        if(!$dialog){$dialog=$automation::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,$condition)}
        if($dialog){break}
        Start-Sleep -Milliseconds 75
    } while ([DateTime]::UtcNow -lt $limit)
    if(!$dialog){throw 'Native import dialog unavailable'}
    try {
        $fields=[System.Windows.Automation.AndCondition]::new(
            [System.Windows.Automation.PropertyCondition]::new($automation::AutomationIdProperty,'1148'),
            [System.Windows.Automation.PropertyCondition]::new($automation::ClassNameProperty,'Edit'))
        do {
            $edit=$dialog.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$fields)
            if($edit){break}
            Start-Sleep -Milliseconds 75
        } while([DateTime]::UtcNow -lt $limit)
        $open=Control $dialog '1'
        if(!$edit -or $edit.Current.ProcessId -ne $session.Process.Id -or $open.Current.ProcessId -ne $session.Process.Id){
            throw 'Could not identify the packaged process file picker controls'
        }
        if([AtlasTestDialog]::SendMessage([IntPtr]$edit.Current.NativeWindowHandle,12,[IntPtr]::Zero,$path) -eq [IntPtr]::Zero){
            throw 'Could not populate native file name'
        }
        if(![AtlasTestDialog]::PostMessage([IntPtr]$dialog.Current.NativeWindowHandle,273,[IntPtr]1,[IntPtr]$open.Current.NativeWindowHandle)){
            throw 'Could not select fixture'
        }
    } catch {
        $dialog.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
        throw
    }
    do {
        if((Control $session.Window 'Notice').Current.Name -like "Imported $name,*"){return}
        Start-Sleep -Milliseconds 75
    } while ([DateTime]::UtcNow -lt $limit)
    throw "Import failed for $name"
}
function Close-Session($session){
    $session.Window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if(!$session.Process.WaitForExit(5000)){throw 'Shutdown timeout'}
}
$session=Launch
$proc=$session.Process
try {
    $module=$proc.Modules | Where-Object ModuleName -eq 'coreclr.dll'
    if(!$module.FileName.StartsWith((Join-Path $payload 'runtime\'),[StringComparison]::OrdinalIgnoreCase)){throw 'External runtime loaded'}
    $window=$session.Window
    $prompt='How long is the archive retention policy?'
    (Control $window 'Prompt').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($prompt)
    Invoke-Control $window 'Search'
    Wait-State $window 'review'
    Invoke-Control $window 'Verify'
    Wait-State $window 'completed'
    if((Control $window 'SourceInfo').Current.Name -notlike 'fact.retention*' -or
        (Control $window 'ChecksInfo').Current.Name -notlike '*Source integrity: passed*Answer relevance: supported*') {
        throw 'Missing packaged source or relevance verdict'
    }
    Invoke-Control $window 'Export'
    $limit=[DateTime]::UtcNow.AddSeconds(8)
    do {
        $file=Get-ChildItem (Join-Path $env:ATLAS_NEXT_PROFILE 'exports') -Filter '*.json' -ErrorAction SilentlyContinue|Select-Object -First 1
        if($file){
            try {
                $receipt=[IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json
                break
            } catch [IO.IOException] {
                # The exporter holds an exclusive handle until the receipt is flushed.
            }
        }
        Start-Sleep -Milliseconds 100
    }while([DateTime]::UtcNow -lt $limit)
    if(!$receipt){throw 'Exported receipt unavailable'}
    if($receipt.request -ne $prompt -or $receipt.evidence.id -ne 'fact.retention' -or
       $receipt.sourceIntegrity -ne 'passed' -or $receipt.answerRelevance -ne 'supported' -or $receipt.result -notlike '*30 days*' -or $receipt.proposal.qualification -ne 'shadow'){throw 'Invalid packaged receipt'}
    Close-Session $session
    $session=Launch
    $proc=$session.Process
    $window=$session.Window
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
    $taskDir=Join-Path $env:ATLAS_NEXT_PROFILE 'tasks'
    $healthyPath=Join-Path $taskDir (([guid]$receipt.id).ToString('N')+'.json')
    $healthyHash=(Get-FileHash -LiteralPath $healthyPath).Hash
    $fixtures=@{}
    function Add-Fixture($name,$content) {
        $path=Join-Path $taskDir $name
        [IO.File]::WriteAllText($path,$content,[Text.UTF8Encoding]::new($false))
        $fixtures[$name]=(Get-FileHash -LiteralPath $path).Hash
    }
    Add-Fixture ([guid]::NewGuid().ToString('N')+'.json') '{'
    Add-Fixture 'invalid-name.json' '{}'
    Add-Fixture ([guid]::NewGuid().ToString('N')+'.json.pending') 'orphaned write'
    foreach($damage in @('events','proposal')){
        $copy=Get-Content -LiteralPath $healthyPath -Raw | ConvertFrom-Json
        $copy.id=[guid]::NewGuid().ToString()
        if($damage -eq 'events'){$copy.events=@()}else{$copy.proposal.sha256='short'}
        Add-Fixture (([guid]$copy.id).ToString('N')+'.json') ($copy | ConvertTo-Json -Depth 10)
    }
    $session=Launch
    $proc=$session.Process
    $window=$session.Window
    Wait-State $window 'interrupted'
    $notice=(Control $window 'RecoveryInfo').Current.Name
    if($notice -notlike 'Recovered 1 interrupted tasks. Quarantined 5 files this startup.*'){throw "Bad recovery notice: $notice"}
    $preserved=@(Get-ChildItem -LiteralPath (Join-Path $env:ATLAS_NEXT_PROFILE 'quarantine') -File -Recurse)
    if($preserved.Count -ne $fixtures.Count -or (Get-FileHash -LiteralPath $healthyPath).Hash -ne $healthyHash){throw 'History preservation failed'}
    foreach($item in $preserved){
        if((Get-FileHash -LiteralPath $item.FullName).Hash -ne $fixtures[$item.Name]){throw "Changed quarantine file $($item.Name)"}
    }
    (Control $window 'ProjectName').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('package-check')
    Invoke-Control $window 'SwitchProject'
    Wait-State $window 'Ready'
    if((Control $window 'ProjectLabel').Current.Name -ne 'Active project: package-check'){throw 'Project selection failed'}
    Import-Document $session 'channel.md' "# Local handbook`r`nRestore signal colour: amber"
    (Control $window 'Prompt').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('What is the restore signal colour?')
    Invoke-Control $window 'Search'
    Wait-State $window 'review'
    Invoke-Control $window 'Verify'
    Wait-State $window 'completed'
    if((Control $window 'Summary').Current.Name -ne 'amber' -or
       (Control $window 'SourceInfo').Current.Name -notlike '*line 2, characters*' -or
       (Control $window 'ResearchInfo').Current.Name -notlike '*Research & Verification: supported*'){throw 'Packaged import span failed'}
    Invoke-Control $window 'Export'
    $importExports=Join-Path $env:ATLAS_NEXT_PROFILE 'exports\project-package-check'
    $limit=[DateTime]::UtcNow.AddSeconds(8)
    do {
        $spanFile=Get-ChildItem -LiteralPath $importExports -Filter '*.json' -ErrorAction SilentlyContinue | Select-Object -First 1
        if($spanFile){
            try { $spanReceipt=[IO.File]::ReadAllText($spanFile.FullName) | ConvertFrom-Json; break } catch [IO.IOException] {}
        }
        Start-Sleep -Milliseconds 100
    } while([DateTime]::UtcNow -lt $limit)
    if($spanReceipt.project -ne 'package-check' -or $spanReceipt.result -ne 'amber' -or $spanReceipt.evidence.span.line -ne 2){throw 'Project receipt export failed'}
    "Imported receipt: $($spanFile.FullName)"
    Import-Document $session 'other.txt' 'Restore signal colour: blue'
    Invoke-Control $window 'Search'
    Wait-State $window 'needs clarification'
    if((Control $window 'ResearchInfo').Current.Name -notlike '*contradiction*'){throw 'Packaged contradiction check failed'}
    "PASS native file picker imports, exact source span and contradictory sources withheld"
    "PowerShell $($PSVersionTable.PSVersion) verified ZIP $((Get-FileHash -LiteralPath $Zip).Hash)"
    "PASS $($inventory.Count) inventory hashes, private runtime, PID/title window discovery on 3 launches"
    "PASS duration question, displayed verdicts, exported receipt, reopen, cancellation and crash recovery"
    "PASS completed history preserved, 5 byte-identical quarantine fixtures and visible counts"
    "Evidence: $($file.FullName)"
} catch {
    if(!$proc.HasExited){$proc.Kill(); [void]$proc.WaitForExit(5000)}
    throw
} finally {
    if(!$proc.HasExited){
        try { Close-Session $session } finally { if(!$proc.HasExited){$proc.Kill()} }
    }
}

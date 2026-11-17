param([string]$DotnetRoot = "$env:ProgramFiles\dotnet", [string]$RuntimeVersion = "10.0.12")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repo = Split-Path $PSScriptRoot -Parent
$out = Join-Path $repo 'out'
$stage = Join-Path $out ("stage-" + [guid]::NewGuid().ToString("N"))
$runtime = Join-Path $stage 'runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
$dotnet = Join-Path $DotnetRoot 'dotnet.exe'
Push-Location $repo
try {
    & $dotnet publish desktop -c Release -o (Join-Path $stage 'app') --configfile NuGet.Config
    if ($LASTEXITCODE) { throw 'Offline publish failed' }
    foreach ($relative in @("host\fxr\$RuntimeVersion","shared\Microsoft.NETCore.App\$RuntimeVersion","shared\Microsoft.WindowsDesktop.App\$RuntimeVersion")) {
        $dest = Join-Path $runtime $relative
        New-Item -ItemType Directory -Path (Split-Path $dest) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $DotnetRoot $relative) -Destination $dest -Recurse
    }
    foreach ($name in @('dotnet.exe','LICENSE.txt','ThirdPartyNotices.txt')) {
        Copy-Item -LiteralPath (Join-Path $DotnetRoot $name) -Destination $runtime
    }
    Copy-Item -LiteralPath 'tools\ATLAS Next.cmd' -Destination $stage
    $hashes = foreach ($file in Get-ChildItem -LiteralPath $stage -Recurse -File) {
        [pscustomobject]@{path=$file.FullName.Substring($stage.Length + 1);sha256=(Get-FileHash -LiteralPath $file.FullName).Hash}
    }
    [IO.File]::WriteAllText((Join-Path $stage 'inventory.json'),($hashes | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
    $tempZip = "$stage.zip"
    [IO.Compression.ZipFile]::CreateFromDirectory($stage,$tempZip,[IO.Compression.CompressionLevel]::Fastest,$false)
    $zip = Join-Path $out 'ATLAS-Next-win-x64.zip'
    if ([IO.File]::Exists($zip)) {
        [IO.File]::Replace($tempZip,$zip,[System.Management.Automation.Language.NullString]::Value)
    } else {
        [IO.File]::Move($tempZip,$zip)
    }
    Get-FileHash $zip | Format-List
} finally { Pop-Location }

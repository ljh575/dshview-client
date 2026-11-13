<#
.SYNOPSIS
    Builds the DSHView runnable folder from source.

.DESCRIPTION
    Reconstructs everything that is not committed on purpose:
      * the fixed-version WebView2 runtime (510MB, cannot live in git)
      * the official Microsoft.Web.WebView2 SDK assemblies
    then compiles DSHView.exe with the in-box .NET Framework C# compiler and
    assembles a ready-to-copy folder (optionally zipped).

    Requires Windows + .NET Framework 4.x (csc.exe ships with Windows) and
    network access to api.nuget.org on the machine that builds. The produced
    folder runs offline.

.EXAMPLE
    pwsh ./build.ps1
    pwsh ./build.ps1 -Zip
#>
[CmdletBinding()]
param(
    [string]$OutDir = (Join-Path $PSScriptRoot 'build/dshview-client'),
    [switch]$Zip,
    # Keep the runtime cache between builds instead of re-downloading 260MB.
    [string]$CacheDir = (Join-Path $PSScriptRoot 'build/.cache')
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$RuntimeVersion = '157.0.4341.46'
$SdkVersion = '1.0.3967.48'

function Write-Step([string]$Message) { Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Note([string]$Message) { Write-Host "    $Message" -ForegroundColor DarkGray }

# NuGet truncated downloads are common behind proxies; resume until complete.
function Get-NuGetPackage([string]$Id, [string]$Version, [string]$Destination) {
    $expected = $null
    $url = "https://api.nuget.org/v3-flatcontainer/$Id/$Version/$Id.$Version.nupkg"
    if (Test-Path $Destination) {
        $head = & curl.exe -s -I --max-time 60 $url 2>$null | Select-String -Pattern '^Content-Length:\s*(\d+)'
        if ($head) { $expected = [int64]$head.Matches[0].Groups[1].Value }
        if ($expected -and (Get-Item $Destination).Length -eq $expected) {
            Write-Note "cached: $Id $Version"
            return
        }
    }
    if (-not (Get-Command curl.exe -ErrorAction SilentlyContinue)) {
        throw 'curl.exe is required (it ships with Windows 10 1803+).'
    }
    $head = & curl.exe -s -I --max-time 60 $url 2>$null | Select-String -Pattern '^Content-Length:\s*(\d+)'
    if ($head) { $expected = [int64]$head.Matches[0].Groups[1].Value }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Destination) | Out-Null
    Remove-Item $Destination -Force -ErrorAction SilentlyContinue
    for ($attempt = 1; $attempt -le 25; $attempt++) {
        & curl.exe -s -L -C - --max-time 240 -o $Destination $url 2>$null | Out-Null
        $size = if (Test-Path $Destination) { (Get-Item $Destination).Length } else { 0 }
        Write-Note ("{0} {1}: {2:N1} MB{3}" -f $Id, $Version, ($size / 1MB), $(if ($expected) { " / $([math]::Round($expected/1MB,1)) MB" } else { '' }))
        if (-not $expected -or $size -ge $expected) { break }
        Start-Sleep -Milliseconds 500
    }
    if ($expected -and (Get-Item $Destination).Length -ne $expected) {
        throw "download incomplete: $Id $Version"
    }
}

function Expand-NuGet([string]$Package, [string]$Destination) {
    Remove-Item $Destination -Recurse -Force -ErrorAction SilentlyContinue
    $zip = [System.IO.Path]::ChangeExtension($Package, '.zip')
    Copy-Item $Package $zip -Force
    Expand-Archive -Path $zip -DestinationPath $Destination -Force
}

Write-Step "Locating the C# compiler"
$csc = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw '.NET Framework 4.x csc.exe not found; install it or run on a stock Windows 10/11.' }
Write-Note $csc

Write-Step "Fetching WebView2 fixed-version runtime ($RuntimeVersion)"
$shellPkg = Join-Path $CacheDir "webview2.runtime.x64.$RuntimeVersion.nupkg"
$corePkg = Join-Path $CacheDir "webview2.runtime.x64.core.$RuntimeVersion.nupkg"
$sdkPkg = Join-Path $CacheDir "microsoft.web.webview2.$SdkVersion.nupkg"
Get-NuGetPackage 'webview2.runtime.x64' $RuntimeVersion $shellPkg
Get-NuGetPackage 'webview2.runtime.x64.core' $RuntimeVersion $corePkg
Get-NuGetPackage 'microsoft.web.webview2' $SdkVersion $sdkPkg

Write-Step 'Extracting packages'
$work = Join-Path $CacheDir 'extracted'
Expand-NuGet $shellPkg (Join-Path $work 'shell')
Expand-NuGet $corePkg (Join-Path $work 'core')
Expand-NuGet $sdkPkg (Join-Path $work 'sdk')

Write-Step "Assembling output: $OutDir"
Remove-Item $OutDir -Recurse -Force -ErrorAction SilentlyContinue
$runtimeDir = Join-Path $OutDir 'webview2'
New-Item -ItemType Directory -Force -Path $runtimeDir | Out-Null

$shellFiles = Join-Path $work 'shell/contentFiles/any/any/WebView2'
$coreFiles = Join-Path $work 'core/contentFiles/any/any/WebView2'
if (-not (Test-Path $shellFiles)) { throw "unexpected package layout: $shellFiles" }
if (-not (Test-Path $coreFiles)) { throw "unexpected package layout: $coreFiles" }
Copy-Item (Join-Path $shellFiles '*') $runtimeDir -Recurse -Force
Copy-Item (Join-Path $coreFiles 'msedge.dll') $runtimeDir -Force

foreach ($required in 'msedgewebview2.exe', 'msedge.dll', "$RuntimeVersion.manifest") {
    if (-not (Test-Path (Join-Path $runtimeDir $required))) { throw "runtime incomplete, missing $required" }
}

$sdkLib = Join-Path $work 'sdk/lib/net462'
$sdkNative = Join-Path $work 'sdk/runtimes/win-x64/native'
Copy-Item (Join-Path $sdkLib 'Microsoft.Web.WebView2.Core.dll') $OutDir -Force
Copy-Item (Join-Path $sdkLib 'Microsoft.Web.WebView2.WinForms.dll') $OutDir -Force
Copy-Item (Join-Path $sdkNative 'WebView2Loader.dll') $OutDir -Force
Copy-Item (Join-Path $PSScriptRoot 'dshview.ini') $OutDir -Force

Write-Step 'Compiling DSHView.exe'
$icon = Join-Path $PSScriptRoot 'src/app_dsh.ico'
& $csc /nologo /target:winexe /platform:anycpu /optimize+ `
    /out:"$OutDir\DSHView.exe" `
    /win32icon:"$icon" /win32manifest:"$PSScriptRoot\src\app.manifest" `
    /resource:"$icon",app_dsh.ico `
    /resource:"$PSScriptRoot\src\app_black.ico",app_black.ico `
    /resource:"$PSScriptRoot\src\app_white.ico",app_white.ico `
    /r:"$OutDir\Microsoft.Web.WebView2.Core.dll" `
    /r:"$OutDir\Microsoft.Web.WebView2.WinForms.dll" `
    /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    "$PSScriptRoot\src\Program.cs"
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }

Copy-Item (Join-Path $PSScriptRoot 'README.md') $OutDir -Force

$size = (Get-ChildItem $OutDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Step ("Ready: {0} ({1:N0} MB)" -f $OutDir, $size)

if ($Zip) {
    $zipPath = Join-Path $PSScriptRoot 'build/dshview-client-win64.zip'
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    & tar.exe -a -c -f $zipPath -C $OutDir . 2>$null
    Write-Step ("Zipped: {0} ({1:N0} MB)" -f $zipPath, ((Get-Item $zipPath).Length / 1MB))
}

Write-Host ''
Write-Host 'Next: copy the folder to the target machine, set url in dshview.ini, run DSHView.exe.' -ForegroundColor Green

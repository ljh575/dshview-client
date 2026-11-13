<#
.SYNOPSIS
    Builds the DSHView runnable folder from source.

.DESCRIPTION
    Reconstructs everything that is not committed on purpose:
      * the fixed-version WebView2 runtime (510MB, cannot live in git)
      * the official Microsoft.Web.WebView2 SDK assemblies
    then compiles DSHView.exe with the in-box .NET Framework C# compiler and
    assembles a ready-to-copy folder (optionally zipped).

    The build is incremental by default: when the output folder already holds a
    complete runtime and the three SDK assemblies, the 248MB download and the
    510MB copy are skipped, and only the executable is recompiled (a couple of
    seconds). Use -Force to rebuild everything from scratch.

    Requires Windows + .NET Framework 4.x (csc.exe ships with Windows). The
    first build also needs network access to api.nuget.org; later builds work
    offline as long as the cache or the output folder is intact. The produced
    folder runs offline either way.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build.ps1
    powershell -ExecutionPolicy Bypass -File .\build.ps1 -Zip
    powershell -ExecutionPolicy Bypass -File .\build.ps1 -Force
#>
[CmdletBinding()]
param(
    [string]$OutDir,
    [switch]$Zip,
    # Keep the runtime cache between builds instead of re-downloading 248MB.
    [string]$CacheDir,
    # Ignore any existing output folder and re-assemble the runtime; the package
    # cache in build/.cache is still reused so this does not re-download anything.
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.IO.Compression.FileSystem

# $PSScriptRoot is empty inside param() defaults on Windows PowerShell 5.1,
# so the script root is resolved here instead.
$ScriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
if (-not $OutDir) { $OutDir = Join-Path $ScriptRoot 'build/dshview-client' }
if (-not $CacheDir) { $CacheDir = Join-Path $ScriptRoot 'build/.cache' }

$RuntimeVersion = '157.0.4341.46'
$SdkVersion = '1.0.3967.48'
$RuntimeMarker = "$RuntimeVersion.manifest"

function Write-Step([string]$Message) { Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Note([string]$Message) { Write-Host "    $Message" -ForegroundColor DarkGray }

function Test-Zip([string]$Path) {
    try {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
        $zip.Dispose()
        return $true
    }
    catch { return $false }
}

function Get-RemoteSize([string]$Url) {
    $head = & curl.exe -s -I --max-time 60 $Url 2>$null | Select-String -Pattern '^Content-Length:\s*(\d+)'
    if ($head) { return [int64]$head.Matches[0].Groups[1].Value }
    return $null
}

# NuGet downloads get truncated behind proxies, so resume until the size matches.
# The package cache is always reused when valid; -Force only rebuilds the output
# folder, it never re-downloads 248MB that is already on disk.
function Get-NuGetPackage([string]$Id, [string]$Version, [string]$Destination) {
    $url = "https://api.nuget.org/v3-flatcontainer/$Id/$Version/$Id.$Version.nupkg"
    if (Test-Path $Destination) {
        $expected = Get-RemoteSize $url
        if ($expected) {
            if ((Get-Item $Destination).Length -eq $expected) {
                Write-Note "cached: $Id $Version"
                return
            }
        }
        elseif (Test-Zip $Destination) {
            # No network: fall back to the local copy if it is a readable archive.
            Write-Note "cached (offline): $Id $Version"
            return
        }
    }
    if (-not (Get-Command curl.exe -ErrorAction SilentlyContinue)) {
        throw 'curl.exe is required (it ships with Windows 10 1803+).'
    }
    $expected = Get-RemoteSize $url
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
    if (-not (Test-Zip $Destination)) { throw "not a valid package: $Id $Version" }
}

function Expand-NuGet([string]$Package, [string]$Destination) {
    Remove-Item $Destination -Recurse -Force -ErrorAction SilentlyContinue
    $zip = [System.IO.Path]::ChangeExtension($Package, '.zip')
    Copy-Item $Package $zip -Force
    Expand-Archive -Path $zip -DestinationPath $Destination -Force
}

function Test-Runtime([string]$Dir) {
    if (-not (Test-Path $Dir)) { return $false }
    foreach ($file in 'msedgewebview2.exe', 'msedge.dll', $RuntimeMarker) {
        if (-not (Test-Path (Join-Path $Dir $file))) { return $false }
    }
    return $true
}

function Test-Sdk([string]$Dir) {
    foreach ($file in 'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll', 'WebView2Loader.dll') {
        if (-not (Test-Path (Join-Path $Dir $file))) { return $false }
    }
    return $true
}

Write-Step 'Locating the C# compiler'
$csc = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw '.NET Framework 4.x csc.exe not found; install it or run on a stock Windows 10/11.' }
Write-Note $csc

$runtimeDir = Join-Path $OutDir 'webview2'
$reuseRuntime = (-not $Force) -and (Test-Runtime $runtimeDir)
$reuseSdk = (-not $Force) -and (Test-Sdk $OutDir)

if ($reuseRuntime -and $reuseSdk) {
    Write-Step 'Reusing the existing runtime and SDK (skipping download and 510MB copy)'
    Write-Note "runtime: $runtimeDir"
    Write-Note 'pass -Force to rebuild them from scratch'
}
else {
    if ($reuseRuntime) { Write-Step 'Runtime present, but the SDK assemblies are missing' }

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
    New-Item -ItemType Directory -Force -Path $runtimeDir | Out-Null

    $shellFiles = Join-Path $work 'shell/contentFiles/any/any/WebView2'
    $coreFiles = Join-Path $work 'core/contentFiles/any/any/WebView2'
    if (-not (Test-Path $shellFiles)) { throw "unexpected package layout: $shellFiles" }
    if (-not (Test-Path $coreFiles)) { throw "unexpected package layout: $coreFiles" }
    Copy-Item (Join-Path $shellFiles '*') $runtimeDir -Recurse -Force
    Copy-Item (Join-Path $coreFiles 'msedge.dll') $runtimeDir -Force

    if (-not (Test-Runtime $runtimeDir)) { throw "runtime incomplete in $runtimeDir" }

    $sdkLib = Join-Path $work 'sdk/lib/net462'
    $sdkNative = Join-Path $work 'sdk/runtimes/win-x64/native'
    Copy-Item (Join-Path $sdkLib 'Microsoft.Web.WebView2.Core.dll') $OutDir -Force
    Copy-Item (Join-Path $sdkLib 'Microsoft.Web.WebView2.WinForms.dll') $OutDir -Force
    Copy-Item (Join-Path $sdkNative 'WebView2Loader.dll') $OutDir -Force
}

Copy-Item (Join-Path $ScriptRoot 'dshview.ini') $OutDir -Force
Copy-Item (Join-Path $ScriptRoot 'README.md') $OutDir -Force
if (Test-Path (Join-Path $ScriptRoot 'README.en.md')) { Copy-Item (Join-Path $ScriptRoot 'README.en.md') $OutDir -Force }
if (Test-Path (Join-Path $ScriptRoot 'LICENSE')) { Copy-Item (Join-Path $ScriptRoot 'LICENSE') $OutDir -Force }
# The README embeds docs/ic-*.png, so the images travel with it.
if (Test-Path (Join-Path $ScriptRoot 'docs')) { Copy-Item (Join-Path $ScriptRoot 'docs') (Join-Path $OutDir 'docs') -Recurse -Force }

Write-Step 'Compiling DSHView.exe'
$icon = Join-Path $ScriptRoot 'src/app_dsh.ico'
& $csc /nologo /target:winexe /platform:anycpu /optimize+ `
    /out:"$OutDir\DSHView.exe" `
    /win32icon:"$icon" /win32manifest:"$ScriptRoot\src\app.manifest" `
    /resource:"$icon",app_dsh.ico `
    /resource:"$ScriptRoot\src\app_black.ico",app_black.ico `
    /resource:"$ScriptRoot\src\app_white.ico",app_white.ico `
    /r:"$OutDir\Microsoft.Web.WebView2.Core.dll" `
    /r:"$OutDir\Microsoft.Web.WebView2.WinForms.dll" `
    /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    "$ScriptRoot\src\Program.cs"
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }

$size = (Get-ChildItem $OutDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Step ("Ready: {0} ({1:N0} MB)" -f $OutDir, $size)

if ($Zip) {
    $zipPath = Join-Path $ScriptRoot 'build/dshview-client-win64.zip'
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    & tar.exe -a -c -f $zipPath -C $OutDir . 2>$null
    Write-Step ("Zipped: {0} ({1:N0} MB)" -f $zipPath, ((Get-Item $zipPath).Length / 1MB))
}

Write-Host ''
Write-Host 'Next: copy the folder to the target machine, set url in dshview.ini, run DSHView.exe.' -ForegroundColor Green

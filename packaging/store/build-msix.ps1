# Builds Kodiak Cleaner's Microsoft Store package (MSIX) on Windows with the Windows SDK's makepri and makeappx.
#
#   build-msix.ps1 -Publish <source\publish> -Out <folder> -Version 0.9.2 [-Store]
#
# Without -Store: KodiakCleaner-<ver>-x64-Test.msix with the unsigned-test identity. Windows 11 installs it without a
#   signature: Add-AppxPackage -Path <file> -AllowUnsigned (in PowerShell as administrator).
# With -Store:    KodiakCleaner-<ver>-x64-Store.msix with the identity in identity.json, unsigned, for the Partner Center
#   upload. Microsoft signs it. Skipped (with a note) while identity.json has no name yet.
param(
    [Parameter(Mandatory)] [string]$Publish,
    [Parameter(Mandatory)] [string]$Out,
    [Parameter(Mandatory)] [string]$Version,
    [switch]$Store
)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot

if ($Store) {
    $id = Get-Content (Join-Path $here "identity.json") -Raw | ConvertFrom-Json
    if (-not $id.name -or -not $id.publisher) {
        Write-Host "identity.json has no Partner Center identity yet; skipping the Store package."
        return
    }
    $name = $id.name; $publisher = $id.publisher; $suffix = "Store"
} else {
    # Windows 11's special publisher for unsigned packages (Add-AppxPackage -AllowUnsigned).
    $name = "KodiakCleaner.Test"
    $publisher = "CN=Kodiak Cleaner Test, OID.2.25.311729368913984317654407730594956997722=1"
    $suffix = "Test"
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must be three numbers, like 0.9.2 (got $Version)." }
$packageVersion = "$Version.0"

# The newest Windows SDK's x64 tools.
$kits = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
$bin = Get-ChildItem $kits -Directory | Where-Object { $_.Name -match '^10\.' -and (Test-Path "$($_.FullName)\x64\makeappx.exe") } |
    Sort-Object { [version]$_.Name } | Select-Object -Last 1
if (-not $bin) { throw "makeappx.exe not found under $kits. Install the Windows SDK." }
$makeappx = "$($bin.FullName)\x64\makeappx.exe"
$makepri = "$($bin.FullName)\x64\makepri.exe"

$work = Join-Path ([IO.Path]::GetTempPath()) ("kc-msix-" + [guid]::NewGuid().ToString("N"))
$stage = Join-Path $work "package"
New-Item -ItemType Directory -Force $stage | Out-Null
try {
    Copy-Item (Join-Path $Publish "KodiakCleaner.exe") $stage
    if (Test-Path (Join-Path $Publish "Docs")) { Copy-Item (Join-Path $Publish "Docs") $stage -Recurse }
    Copy-Item (Join-Path $here "Assets") $stage -Recurse

    $manifest = Get-Content (Join-Path $here "AppxManifest.xml") -Raw
    $manifest = $manifest.Replace('Name="PLACEHOLDER-PartnerCenter.KodiakCleaner"', "Name=`"$name`"")
    $manifest = $manifest.Replace('Publisher="CN=00000000-0000-0000-0000-000000000000"', "Publisher=`"$([Security.SecurityElement]::Escape($publisher))`"")
    $manifest = $manifest.Replace('Version="0.0.0.0"', "Version=`"$packageVersion`"")
    # -cmatch: case-sensitive, so the word "placeholders" in the manifest's own comment doesn't count.
    if ($manifest -cmatch 'Name="PLACEHOLDER-|Publisher="CN=00000000-|Version="0\.0\.0\.0"') { throw "A placeholder in AppxManifest.xml was not filled in." }
    Set-Content (Join-Path $stage "AppxManifest.xml") $manifest -Encoding utf8

    # resources.pri lets Windows pick Square44x44Logo.scale-200.png and the like for "Assets\Square44x44Logo.png".
    $config = Join-Path $work "priconfig.xml"
    & $makepri createconfig /cf $config /dq en-US /pv 10.0.0 /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makepri createconfig failed ($LASTEXITCODE)." }
    & $makepri new /pr $stage /cf $config /mn (Join-Path $stage "AppxManifest.xml") /of (Join-Path $stage "resources.pri") /o
    if ($LASTEXITCODE -ne 0) { throw "makepri new failed ($LASTEXITCODE)." }

    New-Item -ItemType Directory -Force $Out | Out-Null
    $file = Join-Path $Out "KodiakCleaner-$Version-x64-$suffix.msix"
    & $makeappx pack /d $stage /p $file /o
    if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed ($LASTEXITCODE)." }
    Write-Host "Output: $file"
} finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

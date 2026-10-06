# Builds KodiakCleaner.exe, the MSI and the portable ZIP on Windows with the .NET 8 SDK.
# The version and channel come from version.json.
#
#   .\build.ps1            development build: includes the Developer page; packages are named KodiakCleaner-Dev-*
#   .\build.ps1 -Release   release build: no Developer page; refuses to build with the test supporter key
#                          or without a CHANGELOG.md entry for this version
param([switch]$Release)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$info = Get-Content version.json -Raw | ConvertFrom-Json
$version = $info.version
$channel = $info.channel.ToLower()
$label = (Get-Culture).TextInfo.ToTitleCase($channel)
$versionName = if ($channel -eq "stable") { $version } else { "$version $label" }
if ($channel -ne "stable" -and [version]$info.targetStable -gt [version]"1.0.0") { $versionName += " (for $($info.targetStable))" }
# Package names keep the same name every release (Ted's standard, 0.8.31), so GitHub download links keep working:
# KodiakCleaner-Dev-Setup.msi (Docs\Download.url links it), KodiakCleaner-Dev-Setup.zip (the MSI plus Docs) and KodiakCleaner-Dev-Portable.zip. The version is in the
# release tag. Development builds are always Dev.
$product = "KodiakCleaner"
$branch = if (-not $Release) { "Dev" } else { $label }

if ($Release) {
    if (-not (Test-Path keys/supporter-public.pem)) {
        Write-Error "Release builds need Ted's supporter public key (keys/supporter-public.pem). Run: dotnet run --project tools/SupporterCodes -- make-key"
    }
    if (-not (Select-String -Path CHANGELOG.md -SimpleMatch "## [$versionName]" -Quiet)) {
        Write-Error "CHANGELOG.md has no '## [$versionName]' section. Add one before releasing."
    }
}

dotnet test tests/KodiakCleaner.Core.Tests -c Release
if ($LASTEXITCODE) { exit $LASTEXITCODE }

Remove-Item publish -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish src/KodiakCleaner.App -c Release -r win-x64 -o publish -p:ReleaseBuild=$($Release.IsPresent.ToString().ToLower())
if ($LASTEXITCODE) { exit $LASTEXITCODE }

# The seven Docs files (Changelog.txt, Download.url, ...) go in publish\Docs: the MSI installs them and both ZIPs carry them.
dotnet run --project tools/MakeDocs -- publish\Docs $branch
if ($LASTEXITCODE) { exit $LASTEXITCODE }

dotnet build installer/KodiakCleaner.Installer.wixproj -c Release -p:PublishDir="$PSScriptRoot\publish\"
if ($LASTEXITCODE) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force dist | Out-Null
# dist\ gets the bare MSI for the GitHub release (Ted, 0.8.31) and the Setup ZIP with the Docs folder beside it.
$msi = Get-ChildItem installer\bin -Recurse -Filter "$product-$version-x64.msi" | Select-Object -First 1
if (-not $msi) { Write-Error "The MSI wasn't built." }
$stage = Join-Path $env:TEMP "$product-$branch-Setup"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $stage | Out-Null
Copy-Item $msi.FullName "dist\$product-$branch-Setup.msi" -Force
Copy-Item $msi.FullName (Join-Path $stage "$product-$branch-Setup.msi")
Copy-Item publish\Docs $stage -Recurse
Compress-Archive -Path "$stage\*" -DestinationPath "dist\$product-$branch-Setup.zip" -Force
Remove-Item $stage -Recurse -Force

# Portable ZIP: the app plus the Docs folder. Never tests, tools, keys or store assets.
$files = @("publish\KodiakCleaner.exe", "publish\Docs")
Compress-Archive -Path $files -DestinationPath "dist\$product-$branch-Portable.zip" -Force

Get-ChildItem dist | ForEach-Object { Write-Host "Output: $($_.FullName)" }

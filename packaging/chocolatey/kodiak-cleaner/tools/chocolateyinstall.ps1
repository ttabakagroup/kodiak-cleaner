$ErrorActionPreference = 'Stop'
$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

$packageArgs = @{
  packageName    = $env:ChocolateyPackageName
  fileType       = 'msi'
  file64         = Join-Path $toolsDir '__MSI__'
  softwareName   = 'Kodiak Cleaner*'
  silentArgs     = "/qn /norestart /l*v `"$($env:TEMP)\$($env:ChocolateyPackageName).$($env:ChocolateyPackageVersion).MsiInstall.log`""
  validExitCodes = @(0, 3010, 1641)
}

Install-ChocolateyInstallPackage @packageArgs

# The installer is no longer needed once Kodiak Cleaner is installed.
Get-ChildItem $toolsDir -Filter '*.msi' | Remove-Item -Force -ErrorAction SilentlyContinue

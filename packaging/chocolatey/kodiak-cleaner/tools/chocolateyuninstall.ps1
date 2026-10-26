$ErrorActionPreference = 'Stop'

[array]$keys = Get-UninstallRegistryKey -SoftwareName 'Kodiak Cleaner*'

if ($keys.Count -eq 1) {
  Uninstall-ChocolateyPackage -PackageName $env:ChocolateyPackageName -FileType 'msi' `
    -SilentArgs "$($keys[0].PSChildName) /qn /norestart" -ValidExitCodes @(0, 3010, 1605, 1614, 1641)
} elseif ($keys.Count -eq 0) {
  Write-Warning "Kodiak Cleaner is not installed, so there is nothing to uninstall."
} else {
  Write-Warning "$($keys.Count) Kodiak Cleaner entries were found, so nothing was uninstalled. Remove it from Settings > Apps."
}

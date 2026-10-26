$ErrorActionPreference = 'Continue'
$keepPath = Join-Path $env:USERPROFILE 'kodiak-cleaner_temp'

Get-Process -Name 'KodiakCleaner' -ErrorAction SilentlyContinue |
  Where-Object { $_.Path -and $_.Path -like "$env:ChocolateyInstall\lib\*" } |
  Stop-Process -Force -ErrorAction SilentlyContinue

$shortcut = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Kodiak Cleaner (Portable).lnk'
if (Test-Path $shortcut) { Remove-Item $shortcut -Force }

# chocolateyBeforeModify.ps1 has already moved the settings and backups out of the package folder. Leave them,
# since backups may be needed to undo a change, and say where they are.
if (Test-Path $keepPath) {
  Write-Host "Your Kodiak Cleaner settings and backups were kept in $keepPath. Delete that folder if you don't need them."
}

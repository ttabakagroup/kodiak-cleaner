# Runs before an upgrade or uninstall. Closes Kodiak Cleaner and moves the user's settings, backups, logs and reports
# out of the package folder so they survive. chocolateyinstall.ps1 moves them back.
$ErrorActionPreference = 'Continue'
$toolsPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
$keepPath  = Join-Path $env:USERPROFILE 'kodiak-cleaner_temp'

Get-Process -Name 'KodiakCleaner' -ErrorAction SilentlyContinue |
  Where-Object { $_.Path -and $_.Path.StartsWith($toolsPath, [StringComparison]::OrdinalIgnoreCase) } |
  Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

foreach ($name in 'KodiakCleaner.ini', 'Backups', 'Logs', 'Reports') {
  $item = Join-Path $toolsPath $name
  if (Test-Path $item) {
    if (-not (Test-Path $keepPath)) { New-Item -ItemType Directory -Path $keepPath -Force | Out-Null }
    Move-Item -Path $item -Destination $keepPath -Force
  }
}

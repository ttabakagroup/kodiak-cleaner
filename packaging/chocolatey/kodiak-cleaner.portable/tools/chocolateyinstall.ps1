$ErrorActionPreference = 'Stop'
$toolsPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
$keepPath  = Join-Path $env:USERPROFILE 'kodiak-cleaner_temp'

$packageArgs = @{
  packageName    = $env:ChocolateyPackageName
  unzipLocation  = $toolsPath
  url64bit       = 'https://github.com/ttabakagroup/kodiakcleaner/releases/download/__TAG__/__ZIP__'
  checksum64     = '__ZIP_SHA256__'
  checksumType64 = 'sha256'
}

Install-ChocolateyZipPackage @packageArgs

$exe = Join-Path $toolsPath 'KodiakCleaner.exe'

# Put back the settings, backups, logs and reports saved by chocolateyBeforeModify.ps1.
if (Test-Path $keepPath) {
  Get-ChildItem $keepPath | Move-Item -Destination $toolsPath -Force
  Remove-Item $keepPath -Recurse -Force -ErrorAction SilentlyContinue
}

# Kodiak Cleaner runs without admin rights and keeps its settings and backups next to KodiakCleaner.exe.
# The package folder is admin-only, so let normal users write these (never the exe itself), like the installer does.
$ini = Join-Path $toolsPath 'KodiakCleaner.ini'
if (-not (Test-Path $ini)) {
  Set-Content -Path $ini -Encoding UTF8 -Value @(
    '; Kodiak Cleaner settings. Edit with Kodiak Cleaner closed; unknown or invalid values fall back to defaults.',
    '; Kodiak Cleaner fills this file in the first time you change a setting.')
}
& icacls.exe $ini /grant '*S-1-5-32-545:(M)' | Out-Null
foreach ($name in 'Backups', 'Logs', 'Reports') {
  $dir = Join-Path $toolsPath $name
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
  & icacls.exe $dir /grant '*S-1-5-32-545:(OI)(CI)(M)' | Out-Null
}

# Start Menu shortcut for all users. Named (Portable) so it never clashes with the installed version's shortcut.
$shortcut = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Kodiak Cleaner (Portable).lnk'
Install-ChocolateyShortcut -ShortcutFilePath $shortcut -TargetPath $exe -WorkingDirectory $toolsPath -IconLocation $exe

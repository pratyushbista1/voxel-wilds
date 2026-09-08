$ErrorActionPreference = 'Stop'
function Get-SaveChecksum([string]$file) {
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try { [Convert]::ToBase64String($algorithm.ComputeHash([System.IO.File]::ReadAllBytes($file))) }
    finally { $algorithm.Dispose() }
}
$gameRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$cacheRoot = Join-Path $gameRoot '.cache'
$version = (Get-Content -LiteralPath (Join-Path $gameRoot 'package.json') -Raw | ConvertFrom-Json).version
$registryPath = 'HKCU:\Software\34e646bf-ca86-5f39-b459-84e60fb13190'
if (Test-Path -LiteralPath $registryPath) { throw 'An installed copy already exists. Installer testing will not overwrite it.' }
foreach ($shortcut in @((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Voxel Wilds.lnk'), (Join-Path ([Environment]::GetFolderPath('Programs')) 'Voxel Wilds.lnk'))) {
    if (Test-Path -LiteralPath $shortcut) { throw "An existing shortcut would be overwritten: $shortcut" }
}
$testRoot = Join-Path $cacheRoot ('installer-test-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$installDir = [System.IO.Path]::GetFullPath((Join-Path $testRoot 'app'))
if (-not $installDir.StartsWith($cacheRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid test install path.' }
New-Item -ItemType Directory -Path $testRoot | Out-Null
$setup = Join-Path $gameRoot "release\$version\Voxel-Wilds-Setup-$version.exe"
$installer = Start-Process -FilePath $setup -ArgumentList @('/S', '/currentuser', '/no-desktop-shortcut', "/D=$installDir") -WindowStyle Hidden -PassThru -Wait
if ($installer.ExitCode -ne 0) { throw "Installer exited with $($installer.ExitCode)." }
$installedExe = Join-Path $installDir 'Voxel Wilds.exe'
$uninstaller = Join-Path $installDir 'Uninstall Voxel Wilds.exe'
if (-not (Test-Path -LiteralPath $installedExe) -or -not (Test-Path -LiteralPath $uninstaller)) { throw 'Installed game or uninstaller is missing.' }
$registered = (Get-ItemProperty -LiteralPath $registryPath).InstallLocation
if ([System.IO.Path]::GetFullPath($registered) -ne $installDir) { throw 'The registered install path is not the isolated test directory.' }
Write-Output "PASS: Installer created the game and uninstaller in $installDir"
$env:VOXEL_PACKAGED_EXE = $installedExe
$env:VOXEL_TEST_REPORT = Join-Path $gameRoot 'artifacts\installer-desktop-report.json'
Push-Location $gameRoot
try {
    & node scripts/desktop-test.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Installed desktop test failed. The isolated installation was left for inspection.' }
} finally { Pop-Location }
$report = Get-Content -LiteralPath $env:VOXEL_TEST_REPORT -Raw | ConvertFrom-Json
$before = Get-SaveChecksum $report.saveFile
$resolvedInstallDir = (Resolve-Path -LiteralPath $installDir).Path
if ($resolvedInstallDir -ne $installDir -or -not $resolvedInstallDir.StartsWith($cacheRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Refusing to uninstall outside the test directory.' }
$launcher = Join-Path $gameRoot "release\$version\Voxel-Wilds-Uninstall-$version.exe"
$removed = Start-Process -FilePath $launcher -ArgumentList '/S' -WindowStyle Hidden -PassThru -Wait
if ($removed.ExitCode -ne 0) { throw "Uninstaller exited with $($removed.ExitCode)." }
$deadline = [DateTime]::UtcNow.AddSeconds(25)
while ((Test-Path -LiteralPath $installedExe) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }
if (Test-Path -LiteralPath $installedExe) { throw 'Uninstaller did not remove the test game.' }
if (Test-Path -LiteralPath $registryPath) { throw 'Uninstaller did not remove the test registry entry.' }
$after = Get-SaveChecksum $report.saveFile
if ($before -ne $after) { throw 'Saved world changed during uninstall.' }
Write-Output 'PASS: Separate uninstall EXE removed the isolated installation and registry entry, preserving its saved world.'
Write-Output "Preserved test world: $($report.saveFile)"

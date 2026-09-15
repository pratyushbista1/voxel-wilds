param(
    [string]$Version = '2.0.0',
    [switch]$SkipGame
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$gameRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must have three numeric components.' }
$releaseRoot = Join-Path $gameRoot "release\$Version"
$cacheRoot = Join-Path $gameRoot '.cache'
$setupPath = Join-Path $releaseRoot "Voxel-Wilds-Unity-Setup-$Version.exe"
$zipPath = Join-Path $releaseRoot "Voxel-Wilds-Unity-Portable-$Version.zip"
$manifestPath = Join-Path $releaseRoot 'unity-package-manifest.json'
foreach ($path in @($setupPath, $zipPath, $manifestPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Package missing: $path" }
}
if (((Get-Item -LiteralPath $cacheRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'The test cache must not be a junction or symbolic link.' }
function Get-Checksum([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
    finally { $algorithm.Dispose(); $stream.Dispose() }
}
function Invoke-Native([string]$Executable, [string]$Arguments, [int]$TimeoutSeconds = 240) {
    $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -WorkingDirectory $gameRoot -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill()
        $process.WaitForExit()
        throw "Test process timed out: $Executable"
    }
    $code = $process.ExitCode
    $process.Dispose()
    return $code
}
$registryPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\VoxelWildsUnity'
function Get-RegistrySnapshot {
    if (-not (Test-Path -LiteralPath $registryPath)) { return '<absent>' }
    $key = Get-Item -LiteralPath $registryPath
    $values = [ordered]@{}
    foreach ($name in ($key.GetValueNames() | Sort-Object)) { $values[$name] = $key.GetValue($name) }
    return ($values | ConvertTo-Json -Depth 4 -Compress)
}
function Test-InstallChild([string]$Relative) {
    if ([IO.Path]::IsPathRooted($Relative)) { throw 'Absolute payload paths are not allowed.' }
    $full = [IO.Path]::GetFullPath((Join-Path $installDirectory $Relative))
    if (-not $full.StartsWith($installDirectory + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe payload path: $Relative" }
    return $full
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.Version -ne $Version -or $manifest.FileCount -lt 10 -or $manifest.Files.Count -ne $manifest.FileCount) { throw 'Invalid package manifest.' }
if ((Get-Checksum $setupPath) -ne $manifest.SetupSha256 -or (Get-Checksum $zipPath) -ne $manifest.ZipSha256) { throw 'The installer or ZIP changed after packaging.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $expected = @{}
    foreach ($record in $manifest.Files) { $expected[$record.Path.Replace('\', '/')] = $record.Bytes }
    $fileCount = 0
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName.EndsWith('/')) { continue }
        if (-not $expected.ContainsKey($entry.FullName) -or $entry.Length -ne $expected[$entry.FullName]) { throw "Unexpected ZIP payload: $($entry.FullName)" }
        $expected.Remove($entry.FullName)
        $fileCount++
    }
    if ($fileCount -ne $manifest.FileCount -or $expected.Count -ne 0) { throw 'ZIP payload count differs from the manifest.' }
} finally { $archive.Dispose() }
$token = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$testRoot = [IO.Path]::GetFullPath((Join-Path $cacheRoot "unity-installer-test-$token"))
$installDirectory = [IO.Path]::GetFullPath((Join-Path $testRoot 'app'))
if (-not $installDirectory.StartsWith($cacheRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe isolated install target.' }
$artifactDirectory = Join-Path $gameRoot "artifacts\unity-installer-$token"
New-Item -ItemType Directory -Path $testRoot, $artifactDirectory | Out-Null
$registryBefore = Get-RegistrySnapshot
$shortcutPaths = @(
    (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Voxel Wilds Unity Edition.lnk'),
    (Join-Path ([Environment]::GetFolderPath('Programs')) 'Voxel Wilds Unity Edition\Voxel Wilds Unity Edition.lnk'),
    (Join-Path ([Environment]::GetFolderPath('Programs')) 'Voxel Wilds Unity Edition\Uninstall.lnk')
)
$shortcutBefore = @{}
foreach ($path in $shortcutPaths) { $shortcutBefore[$path] = if (Test-Path -LiteralPath $path) { Get-Checksum $path } else { '<absent>' } }
function Assert-HostUnchanged {
    if ((Get-RegistrySnapshot) -ne $registryBefore) { throw 'Test mode changed the normal installation registry.' }
    foreach ($path in $shortcutPaths) {
        $after = if (Test-Path -LiteralPath $path) { Get-Checksum $path } else { '<absent>' }
        if ($after -ne $shortcutBefore[$path]) { throw "Test mode changed an existing shortcut: $path" }
    }
}
$code = Invoke-Native $setupPath ('/S /TESTMODE /D=' + $installDirectory)
if ($code -ne 0) { throw "Installer failed with exit code $code. Isolated files remain at $testRoot" }
Assert-HostUnchanged
$marker = Join-Path $installDirectory 'voxel-wilds-unity.install'
$uninstaller = Join-Path $installDirectory 'Uninstall Voxel Wilds.exe'
if (-not (Test-Path -LiteralPath $uninstaller) -or -not (Test-Path -LiteralPath $marker)) { throw 'Installer did not create its uninstaller and marker.' }
$markerLines = [IO.File]::ReadAllLines($marker)
if ($markerLines.Length -lt 3 -or $markerLines[0] -ne 'VoxelWilds.Unity.Install.v1' -or $markerLines[1] -ne $installDirectory -or $markerLines[2] -ne '1') { throw 'Install marker does not match the isolated test target.' }
foreach ($record in $manifest.Files) {
    $installedPath = Test-InstallChild $record.Path
    if (-not (Test-Path -LiteralPath $installedPath) -or (Get-Checksum $installedPath) -ne $record.Sha256) { throw "Installed file does not match the build: $($record.Path)" }
}
Write-Output "PASS installer extracted $($manifest.FileCount) hash-verified payload files."
$nativeSaves = Join-Path $testRoot 'native-smoke-saves'
New-Item -ItemType Directory -Path $nativeSaves | Out-Null
if (-not $SkipGame) {
    $logPath = Join-Path $artifactDirectory 'Player.log'
    $arguments = '-voxel-smoke -voxel-saves "' + $nativeSaves + '" -voxel-artifacts "' + $artifactDirectory + '" -screen-fullscreen 0 -logFile "' + $logPath + '"'
    $code = Invoke-Native (Join-Path $installDirectory 'Voxel Wilds.exe') $arguments 180
    if ($code -ne 0) { throw "Installed game smoke failed with exit code $code. See $logPath" }
    $resultPath = Join-Path $artifactDirectory 'result.txt'
    if (-not (Test-Path -LiteralPath $resultPath) -or (Get-Content -LiteralPath $resultPath -Raw) -notmatch '^VOXEL_SMOKE_PASS(?:\s|$)') { throw "Installed game smoke did not produce a passing report: $resultPath" }
    Write-Output 'PASS installed Unity game completed its native gameplay smoke checks.'
}
$saveInside = Join-Path $installDirectory 'saves\unity\keep-world.vws'
$saveOutside = Join-Path $nativeSaves 'preserve-sentinel.vws'
$personalFile = Join-Path $installDirectory 'Voxel Wilds_Data\friends-notes.txt'
New-Item -ItemType Directory -Path (Split-Path -Parent $saveInside) -Force | Out-Null
[IO.File]::WriteAllText($saveInside, 'World data retained inside the installation directory.')
[IO.File]::WriteAllText($saveOutside, 'World data retained outside the installation directory.')
[IO.File]::WriteAllText($personalFile, 'This untracked personal file must survive uninstall.')
$preserved = @{}
foreach ($path in @($saveInside, $personalFile) + @(Get-ChildItem -LiteralPath $nativeSaves -Recurse -File | ForEach-Object FullName)) { $preserved[$path] = Get-Checksum $path }
$runner = Join-Path $testRoot 'uninstaller-run.exe'
Copy-Item -LiteralPath $uninstaller -Destination $runner
$wrongDirectory = Join-Path $testRoot 'not-an-installation'
New-Item -ItemType Directory -Path $wrongDirectory | Out-Null
$wrongFile = Join-Path $wrongDirectory 'Voxel Wilds.exe'
[IO.File]::WriteAllText($wrongFile, 'Not a game installation. This file must be untouched.')
$wrongHash = Get-Checksum $wrongFile
$code = Invoke-Native $runner ('/S _?=' + $wrongDirectory)
if ($code -eq 0 -or (Get-Checksum $wrongFile) -ne $wrongHash) { throw 'Uninstaller failed its missing-marker safety check.' }
Write-Output 'PASS uninstaller refuses a directory without its matching installation marker.'
Copy-Item -LiteralPath $marker -Destination (Join-Path $wrongDirectory 'voxel-wilds-unity.install')
$code = Invoke-Native $runner ('/S _?=' + $wrongDirectory)
if ($code -eq 0 -or (Get-Checksum $wrongFile) -ne $wrongHash) { throw 'Uninstaller failed its mismatched-marker safety check.' }
Write-Output 'PASS uninstaller refuses a marker copied from another installation directory.'
$resolvedTarget = (Resolve-Path -LiteralPath $installDirectory).Path
if ($resolvedTarget -ne $installDirectory -or -not $resolvedTarget.StartsWith($cacheRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or ((Get-Item -LiteralPath $resolvedTarget).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Refusing to uninstall an unverified target.' }
$code = Invoke-Native $runner ('/S _?=' + $resolvedTarget)
if ($code -ne 0) { throw "Uninstaller failed with exit code $code." }
foreach ($record in $manifest.Files) { if (Test-Path -LiteralPath (Test-InstallChild $record.Path)) { throw "Installed file remains after uninstall: $($record.Path)" } }
if (Test-Path -LiteralPath $uninstaller) { throw 'The installed uninstaller was not removed.' }
if (Test-Path -LiteralPath $marker) { throw 'The installation marker was not removed.' }
foreach ($path in $preserved.Keys) {
    if (-not (Test-Path -LiteralPath $path) -or (Get-Checksum $path) -ne $preserved[$path]) { throw "Uninstall modified a world or personal file: $path" }
}
Assert-HostUnchanged
$report = [ordered]@{ Status = 'passed'; Version = $Version; InstalledFiles = $manifest.FileCount; GameSmoke = (-not $SkipGame); PreservedFiles = $preserved.Count; TestDirectory = $testRoot; Artifacts = $artifactDirectory; SetupSha256 = $manifest.SetupSha256 }
[IO.File]::WriteAllText((Join-Path $artifactDirectory 'installer-result.json'), ($report | ConvertTo-Json -Depth 3))
Write-Output 'PASS uninstall removed only installed files and retained every test world and personal file.'
Write-Output 'PASS test mode left normal registry entries and shortcuts untouched.'
Write-Output "Preserved test artifacts: $artifactDirectory"
Write-Output "Preserved test worlds: $testRoot"
